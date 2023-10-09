using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Hive.Both.General;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Hive.Network.Tcp;
using Hive.Server.Abstractions;
using Hive.Server.Cluster.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hive.Server.Cluster;

public class CentralizedManagerService : BackgroundService
{
    private readonly TcpAcceptor _tcpAcceptor;
    private readonly IDispatcher _dispatcher;
    private IServiceProvider _serviceProvider;
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
    private readonly Channel<(ISession, NodeLoginReq)> _nodeLoginReqChannel =
        Channel.CreateBounded<(ISession, NodeLoginReq)>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropNewest
        });
    
    public CentralizedManagerService(CentralizedManagerServiceOptions options, IServiceProvider serviceProvider, TcpAcceptor tcpAcceptor,
        IDispatcher dispatcher, ILogger<CentralizedManagerService> logger)
    {
        _serviceProvider = serviceProvider;
        _tcpAcceptor = tcpAcceptor;
        _dispatcher = dispatcher;
        _logger = logger;
        _options = options;

        _tcpAcceptor.OnSessionCreated += OnSessionCreated;
        _tcpAcceptor.OnSessionClosed += OnSessionClosed;

        _dispatcher.AddHandler<ActorHeartBeat>(OnReceiveHeartBeat);
        _dispatcher.AddHandler<NodeLoginReq>(OnReceiveNodeLoginReq);
    }

    private void OnReceiveNodeLoginReq(IDispatcher dispatcher, ISession session, NodeLoginReq message)
    {
        if (!_nodeLoginReqChannel.Writer.TryWrite((session, message)))
        {
            _logger.LogError("Fail to write node login request to channel, request: {@Request}", message);
        }
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
            _logger.LogError("Try to remove service {ServiceName} on node {NodeId} but service not found",
                service.ServiceName, nodeId);
            return;
        }

        foreach (var serviceAddress in serviceAddressBag)
        {
            if (serviceAddress.NodeInfo.NodeId != nodeId)
            {
                _logger.LogError(
                    "Try to remove service {ServiceName} on node {NodeId}, found service address {ServiceAddressNodeId} not match",
                    service.ServiceName, nodeId, serviceAddress.NodeInfo.NodeId);
                continue;
            }

            if (serviceAddressBag.TryTake(out _))
            {
                _logger.LogInformation("Service {ServiceName} on node {NodeId} is removed", service.ServiceName,
                    nodeId);
            }
            else
            {
                _logger.LogError("Fail to remove service {ServiceName} on node {NodeId}", service.ServiceName,
                    nodeId);
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
    
    private void OnReceiveHeartBeat(IDispatcher dispatcher, ISession session, ActorHeartBeat message)
    {
        dispatcher.SendAsync(session, new ActorHeartBeat());
    }

    private void OnSessionCreated(object? sender, OnClientCreatedArgs<TcpSession> e)
    {
        if (_sessionToNodeId.ContainsKey(e.Session.Id))
        {
            _logger.LogError("Session {SessionId} already exists", e.Session.Id);
        }
    }

    private void OnSessionClosed(object? sender, OnClientClosedArgs<TcpSession> e)
    {
        if (_sessionToNodeId.TryRemove(e.Session.Id, out var nodeId))
        {
            _logger.LogInformation("Node {NodeId} disconnected", nodeId);
            TryRemoveNode(nodeId);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ipAddress = IPAddress.Parse(_options.ListenAddress);
        var port = _options.ListenPort;
        var acceptorTsk = _tcpAcceptor.SetupAsync(new IPEndPoint(ipAddress, port), stoppingToken);
        var nodeLoginReqTsk = HandleNodeLoginReqAsync(stoppingToken);
        
        return Task.WhenAll(acceptorTsk, nodeLoginReqTsk);
    }

    private async Task HandleNodeLoginReqAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var (session, message) = await _nodeLoginReqChannel.Reader.ReadAsync(stoppingToken);
            
            _logger.LogDebug("Receive node login request: {@Request}", message);
            
            if (_sessionToNodeId.ContainsKey(session.Id))
            {
                _logger.LogError("Node with session {SessionId} already exists", session.Id);
                continue;
            }
            
            var signature = message.Signature;
            var publicKey = message.PublicKey;

            var endPoint = session.RemoteEndPoint;
            if (endPoint == null)
            {
                _logger.LogError("EndPoint is null");
                continue;
            }

            _logger.LogInformation(
                "Node {EndPointAddress}:{EndPointPort} login with signature {Signature} and public key {PublicKey}",
                endPoint.Address, endPoint.Port, signature, publicKey);


            var nodeId = GetNodeId();
            var services = message.Services;
            _logger.LogInformation("Node {EndPointAddress}:{EndPointPort} has services {Services}", endPoint.Address,
                endPoint.Port, services);
            
            // todo check signature and public key
            
            var nodeInfo = new ClusterNodeInfo(nodeId, endPoint, message.MachineId, services.ToArray());

            if (TryAddNode(nodeInfo))
            {
                var sent = await _dispatcher.SendAsync(session,
                    new NodeLoginResp(ErrorCode.Ok, signature: signature, publicKey: publicKey));

                if (sent)
                {
                    _logger.LogInformation("Node {EndPointAddress}:{EndPointPort} login successfully", endPoint.Address,
                        endPoint.Port);
                    
                    _sessionToNodeId.TryAdd(session.Id, nodeId);
                }
                else
                {
                    _logger.LogError("Node {EndPointAddress}:{EndPointPort} login failed, fail to send response",
                        endPoint.Address, endPoint.Port);
                }
            }
            else
            {
                _logger.LogError("Node {EndPointAddress}:{EndPointPort} login failed, fail to add node", endPoint.Address,
                    endPoint.Port);
                
                await _dispatcher.SendAsync(session,new NodeLoginResp(ErrorCode.NodeAlreadyExists, signature: signature, publicKey: publicKey));
            }
        }
    }

    private int _nodeId = 0;

    private ClusterNodeId GetNodeId()
    {
        Interlocked.Increment(ref _nodeId);
        return new ClusterNodeId(_nodeId);
    }
}