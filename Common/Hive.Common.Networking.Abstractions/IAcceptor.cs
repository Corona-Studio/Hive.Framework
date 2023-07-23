using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions;


public interface IAcceptorImpl<TSession, TSessionId> : IDisposable where TSession : ISession<TSession>
{
    IPEndPoint EndPoint { get; }
    Func<IDataDispatcher<TSession>> DataDispatcherProvider { get; }
    IClientManager<TSessionId, TSession> ClientManager { get; }

    void Start();
    void Stop();
}

/// <summary>
/// 代表一个接入连接接收器
/// </summary>
/// <typeparam name="TSession">分包发送者，通常为对应的 Session</typeparam>
/// <typeparam name="TClient">客户端传输层实现 例如在 TCP 实现下，传输层为 Socket</typeparam>
/// <typeparam name="TSessionId">会话 Id 的类型，用于客户端管理器</typeparam>
public interface IAcceptor<TSession, in TClient, TSessionId> : IAcceptorImpl<TSession, TSessionId>, IDisposable where TSession : ISession<TSession>
{
    ValueTask DoAcceptClient(TClient client, CancellationToken cancellationToken);
}