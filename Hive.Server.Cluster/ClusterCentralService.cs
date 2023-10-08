using System.Collections.Concurrent;
using System.Net;
using Hive.Both.General;
using Hive.Network.Abstractions.Session;
using Hive.Network.Tcp;
using Hive.Server.Cluster.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hive.Server.Cluster;

public class ClusterCentralService: BackgroundService
{
    private readonly TcpAcceptor _tcpAcceptor;
    private readonly IDispatcher _dispatcher;
    private IServiceProvider _serviceProvider;
    private readonly ILogger<ClusterCentralService> _logger;
    
    public ClusterCentralService(IServiceProvider serviceProvider, TcpAcceptor tcpAcceptor, IDispatcher dispatcher, ILogger<ClusterCentralService> logger)
    {
        _serviceProvider = serviceProvider;
        _tcpAcceptor = tcpAcceptor;
        _dispatcher = dispatcher;
        _logger = logger;

        _tcpAcceptor.OnSessionCreated += OnSessionCreated;
        _tcpAcceptor.OnSessionClosed += OnSessionClosed;
        
        _dispatcher.AddHandler<ActorHeartBeat>(OnReceiveHeartBeat);
        _dispatcher.AddHandler<NodeLogin>(OnReceiveNodeLoginReq);
    }

    private void OnReceiveNodeLoginReq(IDispatcher dispatcher, ISession session, NodeLogin message)
    {
        var signature = message.Signature;
        var publicKey = message.PublicKey;
        
        var endPoint = session.RemoteEndPoint as IPEndPoint;
        if (endPoint == null)
        {
            _logger.LogError("EndPoint is null");
            return;
        }
        
        _logger.LogInformation("Node {EndPointAddress}:{EndPointPort} login with signature {Signature} and public key {PublicKey}", 
            endPoint.Address, endPoint.Port, signature, publicKey);
        
        // todo Alloc node id
        var nodeId = 0;
        var services = message.Services;
        _logger.LogInformation("Node {EndPointAddress}:{EndPointPort} has services {Services}", endPoint.Address, endPoint.Port, services);
        
        
        
        
        var sent = dispatcher.SendAsync(session, new NodeLoginResp
        {
            Signature = signature,
            PublicKey = publicKey
        }).Result;

        if (sent)
        {
            _logger.LogInformation("Node {EndPointAddress}:{EndPointPort} login successfully", endPoint.Address, endPoint.Port);
        }
        else
        {
            _logger.LogError("Node {EndPointAddress}:{EndPointPort} login failed, fail to send response", endPoint.Address, endPoint.Port);
        }
        
    }

    private void OnReceiveHeartBeat(IDispatcher dispatcher, ISession session, ActorHeartBeat message)
    {
        dispatcher.SendAsync(session, new ActorHeartBeat());
    }

    private void OnSessionCreated(object? sender, OnClientCreatedArgs<TcpSession> e)
    {
        
    }

    private void OnSessionClosed(object? sender, OnClientClosedArgs<TcpSession> e)
    {
        
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _tcpAcceptor.SetupAsync(new IPEndPoint(IPAddress.Any, 11452), stoppingToken);
    }
}