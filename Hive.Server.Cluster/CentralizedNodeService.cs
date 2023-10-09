using System.Net;
using Hive.Both.General;
using Hive.Network.Abstractions.Session;
using Hive.Network.Tcp;
using Hive.Server.Abstractions;
using Hive.Server.Cluster.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hive.Server.Cluster;

public class CentralizedNodeService: BackgroundService, IClusterNodeService
{
    public ClusterNodeId NodeId { get; }
    private readonly CentralizedNodeServiceOptions _options;
    private ILogger<CentralizedNodeService> _logger;

    private readonly TcpConnector _connector;
    private readonly IDispatcher _dispatcher;
    private ISession? _session;

    public CentralizedNodeService(CentralizedNodeServiceOptions options, ILogger<CentralizedNodeService> logger, TcpConnector connector, IDispatcher dispatcher)
    {
        _options = options;
        _logger = logger;
        _connector = connector;
        _dispatcher = dispatcher;
    }

    public ServiceAddress QueryService(ServiceKey serviceKey)
    {
        throw new NotImplementedException();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var remoteEndPoint = new IPEndPoint(IPAddress.Parse(_options.ManagerAddress), _options.ManagerPort);
        _session = await _connector.ConnectAsync(remoteEndPoint, stoppingToken);
        
        if (_session == null)
        {
            _logger.LogError("Failed to connect to manager");
            return;
        }
        
        // generate private key and public key
        var resp = await _dispatcher.SendAndListenOnce<NodeLoginReq,NodeLoginResp>(_session, new NodeLoginReq()
        {
            // todo parameters
        }, stoppingToken);
         
        if (resp == null)
        {
            _logger.LogError("Failed to login to manager, no response");
            return;
        }
        
        if (resp.ErrorCode != ErrorCode.Ok)
        {
            _logger.LogError("Failed to login to manager, Error Code: {ErrorCode}", resp.ErrorCode);
            return;
        }
        
        await HeartBeatAsync(stoppingToken);
    }
    
    private async Task HeartBeatAsync(CancellationToken stoppingToken)
    {
        if(_session== null)
            throw new InvalidOperationException("Session is null");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await _dispatcher.SendAsync(_session, new ActorHeartBeat());
            
            await Task.Delay(_options.HeartBeatInterval, stoppingToken);
        }
    }
}