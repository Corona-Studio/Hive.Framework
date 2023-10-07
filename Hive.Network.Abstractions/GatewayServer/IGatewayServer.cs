using System;
using System.Threading;
using Hive.Codec.Abstractions;
using Hive.Network.Abstractions.EventArgs;
using Hive.Network.Abstractions.Session;

namespace Hive.Network.Abstractions.GatewayServer;

/// <summary>
///     表示一个网关服务器
/// </summary>
/// <typeparam name="TSession">会话类型，通常为具体协议的实现</typeparam>
/// <typeparam name="TSessionId">会话 ID</typeparam>
/// <typeparam name="TId">封包类型 ID</typeparam>
public interface IGatewayServer<TSession> : IDisposable where TSession : ISession
{
    IPacketCodec PacketCodec { get; }
    IAcceptor<TSession> Acceptor { get; }
    ILoadBalancer LoadBalancer { get; }
    ISecureStreamProvider SecureStreamProvider { get; }

    Func<TSession, ILoadBalancer<TSession>> LoadBalancerGetter { get; }

    bool ServerInitialized { get; }
    event EventHandler<LoadBalancerInitializedEventArgs<TSession>>? OnLoadBalancerInitialized;

    void StartServer(CancellationToken token);
    void StopServer(CancellationToken token);
}