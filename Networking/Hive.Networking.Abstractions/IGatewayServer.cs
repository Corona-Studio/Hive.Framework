using System;
using System.Threading;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions.EventArgs;

namespace Hive.Framework.Networking.Abstractions;

/// <summary>
/// 表示一个网关服务器
/// </summary>
/// <typeparam name="TSession">会话类型，通常为具体协议的实现</typeparam>
/// <typeparam name="TSessionId">会话 ID</typeparam>
/// <typeparam name="TId">封包类型 ID</typeparam>
public interface IGatewayServer<TSession, TSessionId, TId> : IDisposable
    where TSession : ISession<TSession>
    where TId : unmanaged
    where TSessionId : unmanaged
{
    IPacketCodec<TId> PacketCodec { get; }
    IAcceptor<TSession, TSessionId> Acceptor { get; }

    Func<TSession, ILoadBalancer<TSession>> LoadBalancerGetter { get; }
    event EventHandler<LoadBalancerInitializedEventArgs<TSession>>? OnLoadBalancerInitialized;

    void StartServer(CancellationToken token);
    void StopServer(CancellationToken token);

    bool ServerInitialized { get; }
}