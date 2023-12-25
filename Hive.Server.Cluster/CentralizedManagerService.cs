using System.Collections.Concurrent;
using System.Net;
using Hive.Both.General.Channels;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Hive.Server.Abstractions;
using Hive.Server.Cluster.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hive.Server.Cluster;

public class CentralizedManagerService : BackgroundService
{
    private readonly IAcceptor _sessionAcceptor;
    private readonly IDispatcher _dispatcher;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CentralizedManagerService> _logger;
    
    // todo use a better data structure
    
    // SessionId -> ClusterNodeId
    private readonly ConcurrentDictionary<SessionId,ClusterNodeId> _sessionToNodeId = new();
    
    // ClusterNodeId -> ClusterNodeInfo
    private readonly ConcurrentDictionary<ClusterNodeId, ClusterNodeInfo> _clusterNodes = new();
    
    // ServiceKey -> ServiceAddress
    private readonly ConcurrentDictionary<ServiceKey, ConcurrentBag<ServiceAddress>> _serviceKeyToAddresses = new();
    
    private readonly CentralizedManagerServiceOptions _options;
    
    // Save node login request to a channel, and handle it in a background task, so that the acceptor and dispatcher won't be blocked
    private IServerMessageChannel<ActorHeartBeat, ActorHeartBeat> _actorHeartBeatChannel;
    private IServerMessageChannel<NodeLoginReq, NodeLoginResp> _nodeLoginRespChannel;
    public CentralizedManagerService(CentralizedManagerServiceOptions options, IServiceProvider serviceProvider, IAcceptor sessionAcceptor,
        IDispatcher dispatcher, ILogger<CentralizedManagerService> logger)
    {
        _serviceProvider = serviceProvider;
        _sessionAcceptor = sessionAcceptor;
        _dispatcher = dispatcher;
        _logger = logger;
        _options = options;

        _sessionAcceptor.OnSessionCreated += OnSessionCreated;
        _sessionAcceptor.OnSessionClosed += OnSessionClosed;
        _sessionAcceptor.BindTo(dispatcher);

        _actorHeartBeatChannel = _dispatcher.CreateServerChannel<ActorHeartBeat, ActorHeartBeat>();
        _nodeLoginRespChannel = _dispatcher.CreateServerChannel<NodeLoginReq, NodeLoginResp>();
        //_dispatcher.AddHandler<ActorHeartBeat>(OnReceiveHeartBeat);
        //_dispatcher.AddHandler<NodeLoginReq>(OnReceiveNodeLoginReq);
    }
    
    /// <summary>
    /// Try to add a node to the cluster
    /// </summary>
    /// <param name="nodeInfo">The node to add</param>
    /// <returns>True if the node is added successfully, false if the node already exists</returns>
    private bool TryAddNode(ClusterNodeInfo nodeInfo)
    {
        if (!_clusterNodes.TryAdd(nodeInfo.NodeId, nodeInfo))
            return false;

        foreach (var service in nodeInfo.ServiceKeys)
        {
            AddService(service, new ServiceAddress
            {
                ServiceName = service.ServiceName,
                NodeInfo = nodeInfo
            });
        }

        return true;
    }
    
    /// <summary>
    /// Try to remove a node from the cluster
    /// </summary>
    /// <param name="nodeId">The node to remove</param>
    /// <returns>True if the node is removed successfully, false if the node does not exist</returns>
    private bool TryRemoveNode(ClusterNodeId nodeId)
    {
        if (!_clusterNodes.TryGetValue(nodeId, out var nodeInfo))
            return false;

        foreach (var service in nodeInfo.ServiceKeys)
        {
            RemoveService(nodeId, service);
        }

        return _clusterNodes.TryRemove(nodeId, out _);
    }
    
    /// <summary>
    /// Remove a service from the cluster
    /// </summary>
    /// <param name="nodeId">The node that the service is on</param>
    /// <param name="service">The service to remove</param>
    private void RemoveService(ClusterNodeId nodeId, ServiceKey service)
    {
        if (!_serviceKeyToAddresses.TryGetValue(service, out var serviceAddressBag))
        {
            _logger.LogRemoveServiceNotFound(service.ServiceName, nodeId);
            return;
        }

        foreach (var serviceAddress in serviceAddressBag)
        {
            if (serviceAddress.NodeInfo.NodeId != nodeId)
            {
                _logger.LogRemoveServiceAddressNotMatch(service.ServiceName, nodeId, serviceAddress.NodeInfo.NodeId);
                continue;
            }

            if (serviceAddressBag.TryTake(out _))
            {
                _logger.LogRemoveService(service.ServiceName, nodeId);
            }
            else
            {
                _logger.LogRemoveServiceError(service.ServiceName, nodeId);
            }
        }
    }
    
    /// <summary>
    /// Add a service to the cluster
    /// </summary>
    /// <param name="serviceKey">The service to add</param>
    /// <param name="serviceAddress">The address of the service</param>
    private void AddService(ServiceKey serviceKey, ServiceAddress serviceAddress)
    {
        var serviceAddresses = _serviceKeyToAddresses.GetOrAdd(serviceKey, new ConcurrentBag<ServiceAddress>());
        serviceAddresses.Add(serviceAddress);
    }

    private void OnSessionCreated(IAcceptor acceptor, SessionId sessionId, ISession session)
    {
        if (_sessionToNodeId.ContainsKey(sessionId))
        {
            _logger.LogSessionAlreadyExists(sessionId);
        }
    }

    private void OnSessionClosed(IAcceptor acceptor, SessionId sessionId, ISession session)
    {
        if (_sessionToNodeId.TryRemove(sessionId, out var nodeId))
        {
            _logger.LogNodeDisconnected(nodeId);
            TryRemoveNode(nodeId);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ipAddress = IPAddress.Parse(_options.ListenAddress);
        var port = _options.ListenPort;
        var acceptorTsk = _sessionAcceptor.SetupAsync(new IPEndPoint(ipAddress, port), stoppingToken);
        
        var nodeHeartBeatTsk = HandleNodeHeartBeat(stoppingToken);
        var nodeLoginReqTsk = HandleNodeLoginReqAsync(stoppingToken);

        return Task.WhenAll(acceptorTsk, nodeLoginReqTsk, nodeHeartBeatTsk);
    }

    private async Task HandleNodeLoginReqAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var (session, message) = await _nodeLoginRespChannel.ReadAsync(stoppingToken);
            
            _logger.LogReceiveNodeLoginReq(message);
            
            if (_sessionToNodeId.ContainsKey(session.Id))
            {
                _logger.LogNodeAlreadyExists(session.Id);
                continue;
            }
            
            var signature = message.Signature;
            var publicKey = message.PublicKey;

            var endPoint = session.RemoteEndPoint;
            if (endPoint == null)
            {
                _logger.LogEndPointIsNull();
                continue;
            }

            _logger.LogNodeLogin(endPoint.Address, endPoint.Port, signature, publicKey);

            var nodeId = GetNodeId();
            var services = message.Services;

            _logger.LogNodeHasServices(endPoint.Address, endPoint.Port, services);
            
            // todo check signature and public key
            
            var nodeInfo = new ClusterNodeInfo(nodeId, endPoint, message.MachineId, services.ToArray());

            if (TryAddNode(nodeInfo))
            {
                
                var sent = await _nodeLoginRespChannel.WriteAsync(session, new NodeLoginResp(ErrorCode.Ok, signature: signature, publicKey: publicKey));

                if (sent)
                {
                    _logger.LogNodeLoginSuccessfully(endPoint.Address, endPoint.Port);
                    
                    _sessionToNodeId.TryAdd(session.Id, nodeId);
                }
                else
                {
                    _logger.LogNodeLoginFailed(endPoint.Address, endPoint.Port);
                }
            }
            else
            {
                _logger.LogNodeLoginFailed(endPoint.Address, endPoint.Port);
                
                await _nodeLoginRespChannel.WriteAsync(session,new NodeLoginResp(ErrorCode.NodeAlreadyExists, signature: signature, publicKey: publicKey));
            }
        }
    }


    private async Task HandleNodeHeartBeat(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var (session, message) = await _actorHeartBeatChannel.ReadAsync(stoppingToken);
            
            _logger.LogReceiveNodeHeartBeat(message);
            
            if (!_sessionToNodeId.TryGetValue(session.Id, out var nodeId))
            {
                _logger.LogNodeDoesNotExist(session.Id);
                continue;
            }
            
            var nodeInfo = _clusterNodes[nodeId];
            nodeInfo.LastHeartBeatTime = DateTime.UtcNow;
            
            // todo 暂时原样返回
            await _actorHeartBeatChannel.WriteAsync(session, message);
        }
    }

    private int _nodeId = 0;

    private ClusterNodeId GetNodeId()
    {
        Interlocked.Increment(ref _nodeId);
        return new ClusterNodeId(_nodeId);
    }
}

internal static partial class CentralizedManagerServiceLoggers
{
    [LoggerMessage(LogLevel.Error, "Try to remove service {ServiceName} on node {NodeId} but service not found")]
    public static partial void LogRemoveServiceNotFound(this ILogger logger, string serviceName, ClusterNodeId nodeId);

    [LoggerMessage(LogLevel.Error, "Try to remove service {ServiceName} on node {NodeId}, found service address {ServiceAddressNodeId} not match")]
    public static partial void LogRemoveServiceAddressNotMatch(this ILogger logger, string serviceName, ClusterNodeId nodeId, ClusterNodeId serviceAddressNodeId);

    [LoggerMessage(LogLevel.Information, "Service {ServiceName} on node {NodeId} is removed")]
    public static partial void LogRemoveService(this ILogger logger, string serviceName, ClusterNodeId nodeId);

    [LoggerMessage(LogLevel.Error, "Fail to remove service {ServiceName} on node {NodeId}")]
    public static partial void LogRemoveServiceError(this ILogger logger, string serviceName, ClusterNodeId nodeId);

    [LoggerMessage(LogLevel.Warning, "Session {SessionId} already exists")]
    public static partial void LogSessionAlreadyExists(this ILogger logger, SessionId sessionId);

    [LoggerMessage(LogLevel.Information, "Node {NodeId} disconnected")]
    public static partial void LogNodeDisconnected(this ILogger logger, ClusterNodeId nodeId);

    [LoggerMessage(LogLevel.Debug, "Receive node login request: {Request}")]
    public static partial void LogReceiveNodeLoginReq(this ILogger logger, NodeLoginReq request);

    [LoggerMessage(LogLevel.Error, "Node with session {SessionId} already exists")]
    public static partial void LogNodeAlreadyExists(this ILogger logger, SessionId sessionId);

    [LoggerMessage(LogLevel.Error, "EndPoint is null")]
    public static partial void LogEndPointIsNull(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Node {EndPointAddress}:{EndPointPort} login with signature {Signature} and public key {PublicKey}")]
    public static partial void LogNodeLogin(this ILogger logger, IPAddress endPointAddress, int endPointPort, string signature, string publicKey);

    [LoggerMessage(LogLevel.Information, "Node {EndPointAddress}:{EndPointPort} has services {Services}")]
    public static partial void LogNodeHasServices(this ILogger logger, IPAddress endPointAddress, int endPointPort, IReadOnlyList<ServiceKey> services);

    [LoggerMessage(LogLevel.Information, "Node {EndPointAddress}:{EndPointPort} login successfully")]
    public static partial void LogNodeLoginSuccessfully(this ILogger logger, IPAddress endPointAddress, int endPointPort);

    [LoggerMessage(LogLevel.Error, "Node {EndPointAddress}:{EndPointPort} login failed, fail to send response")]
    public static partial void LogNodeLoginFailed(this ILogger logger, IPAddress endPointAddress, int endPointPort);

    [LoggerMessage(LogLevel.Debug, "Receive node heart beat: {Request}")]
    public static partial void LogReceiveNodeHeartBeat(this ILogger logger, ActorHeartBeat request);

    [LoggerMessage(LogLevel.Error, "Node with session {SessionId} does not exist")]
    public static partial void LogNodeDoesNotExist(this ILogger logger, SessionId sessionId);
}