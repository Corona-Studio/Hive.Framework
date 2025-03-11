using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Network.Abstractions.Session;

public interface IAcceptor : IDisposable
{
    event Func<ISession, ValueTask> OnSessionCreateAsync;

    event Action<IAcceptor, SessionId, ISession> OnSessionCreated;

    /// <summary>
    ///     连接意外关闭时触发，主动关闭时不会触发
    /// </summary>
    event Action<IAcceptor, SessionId, ISession> OnSessionClosed;

    ISession? GetSession(SessionId sessionId);
    Task SetupAsync(IPEndPoint listenEndPoint, CancellationToken token);
    Task StartAcceptLoop(CancellationToken token);
    Task<bool> TryCloseAsync(CancellationToken token);
    ValueTask<bool> TryDoOnceAcceptAsync(CancellationToken token);

    ValueTask<bool> TrySendToAsync(SessionId sessionId, MemoryStream buffer, CancellationToken token = default);
    ValueTask SendToAsync(SessionId sessionId, MemoryStream buffer, CancellationToken token = default);
}

/// <summary>
///     代表一个接入连接接收器
/// </summary>
public interface IAcceptor<TSession> : IAcceptor where TSession : ISession
{
    bool IsValid { get; }

    /// <summary>
    ///     True if the acceptor is running in a separate thread and managed by itself.
    /// </summary>
    bool IsSelfRunning { get; }

    new event Func<TSession, ValueTask> OnSessionCreateAsync;

    new event Action<IAcceptor, SessionId, TSession> OnSessionCreated;

    /// <summary>
    ///     连接意外关闭时触发，主动关闭时不会触发
    /// </summary>
    new event Action<IAcceptor, SessionId, TSession> OnSessionClosed;

    new TSession? GetSession(SessionId sessionId);
}