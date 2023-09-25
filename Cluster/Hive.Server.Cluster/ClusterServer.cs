using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;

namespace Hive.Server.Cluster;

public class ClusterServer : AbstractServer<ushort,TcpSession<ushort>,ushort>
{
    public ClusterServer(IPacketCodec codec, IPacketIdMapper<ushort> mapper, IClientManager<ushort, TcpSession<ushort>> clientManager, IDataDispatcher<TcpSession<ushort>> dispatcher) : base(codec, mapper, clientManager, dispatcher)
    {
    }

    public override Task StartAsync(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    public override ValueTask DoAccept()
    {
        return ValueTask.CompletedTask;
    }
}