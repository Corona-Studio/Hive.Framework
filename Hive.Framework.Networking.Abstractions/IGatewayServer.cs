using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Networking.Abstractions;

/// <summary>
/// 表示一个网关服务器
/// </summary>
/// <typeparam name="TSession">会话类型，通常为具体协议的实现</typeparam>
/// <typeparam name="TSessionId">会话 ID</typeparam>
/// <typeparam name="TId">封包类型 ID</typeparam>
public interface IGatewayServer<TSession, TSessionId, TId> where TSession : ISession<TSession> where TId : unmanaged
{
    IPacketCodec<TId> PacketCodec { get; }
    IAcceptorImpl<TSession, TSessionId> Acceptor { get; }
    TId[]? ExcludeRedirectPacketIds { get; }

    void StartServer();
    void StopServer();

    bool ServerInitialized { get; }
}