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
public abstract class AbstractAcceptor<TClient, TSession, TId> : IAcceptor<TSession, TClient>, IHasCodec<TId>
{
    public IPEndPoint EndPoint { get; }
    public IEncoder<TId> Encoder { get; }
    public IDecoder<TId> Decoder { get; }
    public IDataDispatcher<TSession> DataDispatcher { get; }

    protected readonly CancellationTokenSource CancellationTokenSource = new ();

    public AbstractAcceptor(IPEndPoint endPoint, IEncoder<TId> encoder, IDecoder<TId> decoder, IDataDispatcher<TSession> dataDispatcher)
    {
        EndPoint = endPoint;
        Encoder = encoder;
        Decoder = decoder;
        DataDispatcher = dataDispatcher;
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