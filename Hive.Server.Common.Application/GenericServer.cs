using System.Net;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Hive.Server.Common.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Server.Common.Application;

public class GenericServer : IServer
{
    private readonly IServiceProvider _serviceProvider;
    private IAcceptor? _acceptor;
    private IServerApplication? _application;
    private IDispatcher _dispatcher;
    
    private readonly ILogger<GenericServer> _logger;
    private CancellationTokenSource? _acceptorLoopTokenSource;
    public GenericServer(IServiceProvider serviceProvider, IDispatcher dispatcher, ILogger<GenericServer> logger)
    {
        _serviceProvider = serviceProvider;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task StartAsync(string key, IPEndPoint ipEndPoint, IServerApplication app, CancellationToken stoppingToken)
    {
        _application = app;
        _acceptor = _serviceProvider.GetRequiredKeyedService<IAcceptor>(key);
        _acceptor.OnSessionCreated += OnSessionCreated;
        _acceptor.OnSessionClosed += OnSessionClosed;

        await _acceptor.SetupAsync(ipEndPoint, stoppingToken);
        
        _logger.LogServerStarted(ipEndPoint);
        
        _acceptorLoopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _acceptor.StartAcceptLoop(_acceptorLoopTokenSource.Token);
        
        await _application.OnStartAsync(_acceptor,_dispatcher,_acceptorLoopTokenSource.Token);
    }

    private void OnSessionClosed(IAcceptor acceptor, SessionId sessionId, ISession session)
    {
        _application?.OnSessionCreated(session);
    }

    private void OnSessionCreated(IAcceptor acceptor, SessionId sessionId, ISession session)
    {
        _application?.OnSessionClosed(session);
    }
    

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _acceptorLoopTokenSource?.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _acceptorLoopTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal static partial class GenericServerLoggers
{
    [LoggerMessage(LogLevel.Information, "Server started at {IpEndPoint}")]
    public static partial void LogServerStarted(this ILogger logger, IPEndPoint ipEndPoint);
}