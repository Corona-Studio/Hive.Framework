using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;

namespace Hive.Framework.Networking.Shared;

/// <summary>
/// 链接接收器抽象
/// </summary>
/// <typeparam name="TClient">客户端传输层实现 例如在 TCP 实现下，传输层为 Socket</typeparam>
/// <typeparam name="TSession">连接会话类型 例如在 TCP 实现下，其类型为 TcpSession{TId}</typeparam>
/// <typeparam name="TId">封包 ID 类型（通常为 ushort）</typeparam>
/// <typeparam name="TSessionId">会话 Id 的类型，用于客户端管理器</typeparam>
public abstract class AbstractAcceptor<TClient, TSession, TId, TSessionId> : IAcceptor<TSession, TClient, TSessionId>, IHasCodec<TId> where TSession : ISession<TSession> where TId : unmanaged
{
    public IPEndPoint EndPoint { get; }
    public IPacketCodec<TId> PacketCodec { get; }
    public IDataDispatcher<TSession> DataDispatcher { get; }
    public IClientManager<TSessionId, TSession> ClientManager { get; }

    protected readonly CancellationTokenSource CancellationTokenSource = new ();

    protected AbstractAcceptor(
        IPEndPoint endPoint,
        IPacketCodec<TId> packetCodec,
        IDataDispatcher<TSession> dataDispatcher,
        IClientManager<TSessionId, TSession> clientManager)
    {
        EndPoint = endPoint;
        PacketCodec = packetCodec;
        DataDispatcher = dataDispatcher;
        ClientManager = clientManager;
    }

    public abstract void Start();

    public abstract void Stop();

    public abstract ValueTask DoAcceptClient(TClient client, CancellationToken cancellationToken);

    public virtual void Dispose()
    {
        CancellationTokenSource.Cancel();
        CancellationTokenSource.Dispose();
    }
}