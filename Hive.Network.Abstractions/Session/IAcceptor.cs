using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Network.Abstractions.Session;

public interface IAcceptor : IDisposable
{
    event Func<ISession, ValueTask> OnSessionCreateAsync;

    event EventHandler<OnClientCreatedArgs<ISession>> OnSessionCreated;

    /// <summary>
    ///     连接意外关闭时触发，主动关闭时不会触发
    /// </summary>
    event EventHandler<OnClientClosedArgs<ISession>> OnSessionClosed;
    
    ISession? GetSession(SessionId sessionId);
    Task<bool> SetupAsync(IPEndPoint listenEndPoint, CancellationToken token);
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

    void StartAcceptLoop(CancellationToken token);

    new Task<bool> CloseAsync(CancellationToken token);

    new ValueTask<bool> DoOnceAcceptAsync(CancellationToken token);

    new event EventHandler<OnClientCreatedArgs<TSession>> OnSessionCreated;

    /// <summary>
    ///     连接意外关闭时触发，主动关闭时不会触发
    /// </summary>
    new event EventHandler<OnClientClosedArgs<TSession>> OnSessionClosed;

    new TSession? GetSession(SessionId sessionId);

    ValueTask<bool> SendToAsync(SessionId sessionId, MemoryStream buffer, CancellationToken token = default);

    /// <summary>
    ///     进行一次心跳检查
    ///     <para>作为客户端的一方主动发送心跳包，SessionManager记录受到心跳包的时间</para>
    ///     <para>作为服务端的一方主动检查心跳包，如果超过一定时间没有收到心跳包，则断开连接</para>
    /// </summary>
    /// <returns></returns>
    void DoHeartBeatCheck();
}

public readonly struct OnClientCreatedArgs<TSession> where TSession : ISession
{
    public readonly SessionId SessionId;
    public readonly TSession Session;

    public OnClientCreatedArgs(SessionId sessionId, TSession session)
    {
        SessionId = sessionId;
        Session = session;
    }
}

public readonly struct OnClientClosedArgs<TSession> where TSession : ISession
{
    public readonly SessionId SessionId;
    public readonly TSession Session;

    public OnClientClosedArgs(SessionId sessionId, TSession session)
    {
        SessionId = sessionId;
        Session = session;
    }
}