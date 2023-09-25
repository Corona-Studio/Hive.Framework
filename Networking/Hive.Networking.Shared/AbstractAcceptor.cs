using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;

namespace Hive.Framework.Networking.Shared;

/// <summary>
/// 链接接收器抽象
/// </summary>
/// <typeparam name="TSocket">客户端传输层实现 例如在 TCP 实现下，传输层为 Socket</typeparam>
/// <typeparam name="TSession">连接会话类型 例如在 TCP 实现下，其类型为 TcpSession{TId}</typeparam>
/// <typeparam name="TId">封包 ID 类型（通常为 ushort）</typeparam>
/// <typeparam name="TSessionId">会话 Id 的类型，用于客户端管理器</typeparam>
public abstract class AbstractAcceptor<TSocket, TSession, TId, TSessionId> : IAcceptor<TSession, TSessionId>, IHasCodec<TId> where TSession : ISession<TSession>
    where TId : unmanaged
    where TSessionId : unmanaged
{
    protected AbstractAcceptor(IPEndPoint endPoint, IPacketCodec<TId> codec, IDataDispatcher<TSession> dataDispatcher, IClientManager<TSessionId, TSession> clientManager, ISessionCreator<TSession, TSocket> sessionCreator)
    {
        EndPoint = endPoint;
        Codec = codec;
        DataDispatcher = dataDispatcher;
        ClientManager = clientManager;
        SessionCreator = sessionCreator;
    }

    public IPEndPoint EndPoint { get; }
    public IPacketCodec<TId> Codec { get; }
    public IDataDispatcher<TSession> DataDispatcher { get; }
    public IClientManager<TSessionId, TSession> ClientManager { get; }

    public ISessionCreator<TSession, TSocket> SessionCreator { get; }

    public virtual bool IsValid { get; }
    public bool IsSelfRunning { get; protected set; }
    
    
    public abstract Task<bool> SetupAsync(CancellationToken token);

    public virtual Task StartAcceptLoop(CancellationToken token)
    {
        return Task.Run(async () =>
        {
            IsSelfRunning = true;
            while (!token.IsCancellationRequested)
            {
                await DoAcceptAsync(token);
            }
            IsSelfRunning = false;
        }, token);
    }


    public abstract Task<bool> CloseAsync(CancellationToken token);

    public abstract ValueTask<bool> DoAcceptAsync(CancellationToken token);
}