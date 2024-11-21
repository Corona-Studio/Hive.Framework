using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions;
using Hive.Network.Shared.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Tcp;

public sealed class TcpAcceptor : AbstractAcceptor<TcpSession>
{
    private readonly ObjectFactory<TcpSession> _sessionFactory;
    private Socket? _serverSocket;

    public TcpAcceptor(
        IServiceProvider serviceProvider,
        ILogger<TcpAcceptor> logger)
        : base(serviceProvider, logger)
    {
        _sessionFactory = ActivatorUtilities.CreateFactory<TcpSession>([typeof(int), typeof(Socket)]);
    }

    public override IPEndPoint? EndPoint => _serverSocket?.LocalEndPoint as IPEndPoint;
    public override bool IsValid => _serverSocket != null;

    private void InitSocket(IPEndPoint listenEndPoint)
    {
        _serverSocket = new Socket(listenEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
    }

    public override Task SetupAsync(IPEndPoint listenEndPoint, CancellationToken token)
    {
        if (_serverSocket == null)
            InitSocket(listenEndPoint);

        if (_serverSocket == null)
            throw new NullReferenceException("ServerSocket is null and InitSocket failed.");

        _serverSocket.Bind(listenEndPoint);
        _serverSocket.Listen(listenEndPoint.Port);

        return Task.CompletedTask;
    }

    public override Task<bool> TryCloseAsync(CancellationToken token)
    {
        if (_serverSocket == null) return Task.FromResult(false);

        _serverSocket.Close();
        _serverSocket.Dispose();
        _serverSocket = null;

        return Task.FromResult(true);
    }


    public override async ValueTask<bool> TryDoOnceAcceptAsync(CancellationToken token)
    {
        if (_serverSocket == null)
            return false;

        var acceptSocket = await _serverSocket.AcceptAsync();
        CreateSession(acceptSocket);

        return true;
    }

    private void CreateSession(Socket acceptSocket)
    {
        var sessionId = GetNextSessionId();
        var clientSession = _sessionFactory.Invoke(ServiceProvider, [sessionId, acceptSocket]);
        clientSession.OnSocketError += OnSocketError;
        FireOnSessionCreate(clientSession);
    }

    private void OnSocketError(object sender, SocketError e)
    {
        if (sender is TcpSession session)
        {
            Logger.LogSocketError(session.Id, e);
            session.Close();
            FireOnSessionClosed(session);
        }
    }

    public override void Dispose()
    {
        _serverSocket?.Dispose();
    }
}

internal static partial class TcpAcceptorLoggers
{
    [LoggerMessage(LogLevel.Debug, "Session {sessionId} socket error: {socketError}")]
    public static partial void LogSocketError(this ILogger logger, SessionId sessionId, SocketError socketError);
}