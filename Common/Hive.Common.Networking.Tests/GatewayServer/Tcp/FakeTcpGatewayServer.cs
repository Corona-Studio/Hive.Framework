using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Abstractions.EventArgs;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Tests.GatewayServer.Tcp;

public class FakeTcpGatewayServer : AbstractGatewayServer<TcpSession<ushort>, Guid, ushort>
{
    public FakeTcpGatewayServer(
        IPacketCodec<ushort> packetCodec,
        IAcceptorImpl<TcpSession<ushort>, Guid> acceptor,
        Func<TcpSession<ushort>, ILoadBalancer<TcpSession<ushort>>> loadBalancerGetter) : base(packetCodec, acceptor, loadBalancerGetter)
    {
    }

    public int RegisteredForwardPacketCount { get; private set; }

    protected override void RegisterServerRegistrationMessage(TcpSession<ushort> session)
    {
        session.OnReceive<ServerRegistrationMessage>((message, tcpSession) =>
        {
            foreach (var packetId in message.Payload.PackagesToReceive)
            {
                AddPacketRoute(packetId, tcpSession);
                RegisterServerReplyMessage(tcpSession);
                RegisteredForwardPacketCount++;
            }
        });
    }

    protected override void RegisterServerReplyMessage(TcpSession<ushort> session)
    {
        session.OnReceive<DefaultServerReplyPacket>(async (message, _) =>
        {
            await DoForwardDataToClientAsync(message.Payload);
        });
    }

    protected override async void NotifyClientCanStartTransmitMessage(TcpSession<ushort> session)
    {
        await session.SendAsync(new ClientCanTransmitMessage(), PacketFlags.None);
    }

    protected override void RegisterClientStartTransmitMessage(TcpSession<ushort> session)
    {
        session.OnReceive<ClientStartTransmitMessage>((message, tcpSession) =>
        {
            tcpSession.RedirectPacketIds = message.Payload.RedirectPacketIds.ToHashSet();
            tcpSession.OnDataReceived += TcpSessionOnOnDataReceived;
            tcpSession.RedirectReceivedData = true;

            NotifyClientCanStartTransmitMessage(session);
        });
    }

    private async Task TcpSessionOnOnDataReceived(object? sender, ReceivedDataEventArgs e)
    {
        await DoForwardDataToServerAsync((TcpSession<ushort>)sender!, e.Data);
    }

    protected override void InvokeOnClientDisconnected(object sender, ClientConnectionChangedEventArgs<TcpSession<ushort>> e)
    {
    }
}