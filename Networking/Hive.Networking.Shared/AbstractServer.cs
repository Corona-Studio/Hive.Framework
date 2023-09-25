using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;

namespace Hive.Framework.Networking.Shared;

public abstract class AbstractServer<TClientId,TSession, TPacketId> : IServer<TClientId, TPacketId> where TPacketId : unmanaged where TSession : ISession<TSession>
{
    protected IPacketCodec Codec;
    protected IPacketIdMapper<TPacketId> Mapper;
    protected IClientManager<TPacketId, TSession> ClientManager;
    protected IDataDispatcher<TSession> Dispatcher;
    
    protected AbstractServer(IPacketCodec codec, IPacketIdMapper<TPacketId> mapper, IClientManager<TPacketId, TSession> clientManager, IDataDispatcher<TSession> dispatcher)
    {
        Codec = codec;
        Mapper = mapper;
        ClientManager = clientManager;
        Dispatcher = dispatcher;
    }
    
    public virtual Task StartAcceptLoop(CancellationToken token)
    {
        return Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await DoAccept();
            }
        }, token);
    }

    public abstract Task StartAsync(CancellationToken token);

    public abstract Task StopAsync(CancellationToken token);

    public abstract ValueTask DoAccept();
}