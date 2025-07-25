﻿using System.Buffers;
using System.Collections.Concurrent;
using Hive.Both.General.Dispatchers;
using Hive.Both.Messages.C2S;
using Hive.Both.Messages.S2C;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Hive.Server.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hive.Server.Default;

public class DefaultClientService<TSession> : BackgroundService, IClientService where TSession : ISession
{
    private readonly IAcceptor<TSession> _acceptor;
    private readonly ConcurrentDictionary<ClientId, ClientHandle> _clientDict = new();
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<DefaultClientService<TSession>> _logger;
    private readonly ClientServiceOptions _options;
    private readonly ConcurrentDictionary<SessionId, ClientHandle> _sessionIdToClientDict = new();

    private int _curClientId;

    public DefaultClientService(ClientServiceOptions options, IAcceptor<TSession> acceptor,
        ILogger<DefaultClientService<TSession>> logger, IDispatcher dispatcher)
    {
        _options = options;
        _acceptor = acceptor;
        _logger = logger;
        _dispatcher = dispatcher;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogStartingClientService();
        await base.StartAsync(cancellationToken);

        _logger.LogRegisteringHandlers();
        _dispatcher.AddHandler<CSHeartBeat>(OnReceiveClientHeartBeat);

        _logger.LogStartingAcceptor();
        _acceptor.OnSessionCreated += OnSessionCreated;
        _acceptor.OnSessionClosed += OnSessionClosed;

        await _acceptor.SetupAsync(_options.EndPoint, cancellationToken);
    }

    public ClientHandle? GetClientHandle(ClientId clientId)
    {
        return _clientDict.GetValueOrDefault(clientId);
    }

    public void KickClient(ClientId clientId)
    {
    }

    private void OnSessionClosed(IAcceptor acceptor, SessionId sessionId, TSession session)
    {
        if (_sessionIdToClientDict.TryRemove(sessionId, out var clientHandle))
            _clientDict.TryRemove(clientHandle.Id, out _);
    }

    private void OnSessionCreated(IAcceptor acceptor, SessionId sessionId, TSession session)
    {
        var clientId = GenerateClientId();
        var clientHandle = new ClientHandle(clientId, session);
        session.OnMessageReceived += OnReceiveMessage;

        _clientDict.TryAdd(clientId, clientHandle);
        _sessionIdToClientDict.TryAdd(sessionId, clientHandle);
    }

    private void OnReceiveMessage(object? sender, ReadOnlySequence<byte> e)
    {
        if (sender is not TSession session)
            return;

        _dispatcher.Dispatch(session, e);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _acceptor.StartAcceptLoop(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var utcNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var (id, clientHandle) in _clientDict)
                if (utcNow - clientHandle.LastHeartBeatTimeUtc > _options.HeartBeatTimeout)
                    KickClient(id);
            await Task.Delay(500, stoppingToken);
        }
    }

    private void OnReceiveClientHeartBeat(MessageContext<CSHeartBeat> context)
    {
        if (_sessionIdToClientDict.TryGetValue(context.FromSession.Id, out var clientHandle))
        {
            clientHandle.LastHeartBeatTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _dispatcher.SendAsync(clientHandle.Session, new SCHeartBeat());
        }
    }

    public void SendMessage(ClientId clientId, object message)
    {
        if (_clientDict.TryGetValue(clientId, out var clientHandle))
            _dispatcher.SendAsync(clientHandle.Session, message);
    }

    private ClientId GenerateClientId()
    {
        Interlocked.Increment(ref _curClientId);
        return new ClientId { Id = _curClientId };
    }
}

internal static partial class DefaultClientServiceLoggers
{
    [LoggerMessage(LogLevel.Information, "Starting client service...")]
    public static partial void LogStartingClientService(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Registering handlers...")]
    public static partial void LogRegisteringHandlers(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Starting acceptor...")]
    public static partial void LogStartingAcceptor(this ILogger logger);
}