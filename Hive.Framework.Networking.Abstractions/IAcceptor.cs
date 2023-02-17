using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions;

/// <summary>
/// 代表一个接入连接接收器
/// </summary>
/// <typeparam name="TSender">分包发送者，通常为对应的 Session</typeparam>
/// <typeparam name="TClient">客户端传输层实现 例如在 TCP 实现下，传输层为 Socket</typeparam>
/// <typeparam name="TSessionId">会话 Id 的类型，用于客户端管理器</typeparam>
/// <typeparam name="THeartBeat">心跳包的类型</typeparam>
public interface IAcceptor<TSender, in TClient, TSessionId> : IDisposable
{
    IPEndPoint EndPoint { get; }
    IDataDispatcher<TSender> DataDispatcher { get; }
    IClientManager<TSessionId, TSender> ClientManager { get; }

    void Start();
    void Stop();

    ValueTask DoAcceptClient(TClient client, CancellationToken cancellationToken);
}