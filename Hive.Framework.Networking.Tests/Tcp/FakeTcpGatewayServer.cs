using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Abstractions.EventArgs;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Networking.Tests.Messages;

namespace Hive.Framework.Networking.Tests.Tcp;

public class FakeTcpGatewayServer : AbstractGatewayServer<TcpSession<ushort>, Guid, ushort>
{
    public FakeTcpGatewayServer(
        IPacketCodec<ushort> packetCodec,
        IAcceptorImpl<TcpSession<ushort>, Guid> acceptor,
        ushort[]? excludeRedirectPacketIds,
        Func<TcpSession<ushort>, ILoadBalancer<TcpSession<ushort>>> loadBalancerGetter) : base(packetCodec, acceptor, excludeRedirectPacketIds, loadBalancerGetter)
    {
    }

    protected override void RegisterServerRegistrationMessage(TcpSession<ushort> session)
    {
        session.OnReceive<ServerRegistrationMessage>((message, tcpSession) =>
        {
            foreach (var packetId in message.PackagesToReceive)
            {
                AddPacketRoute(packetId, tcpSession);
            }
        });
    }

    protected override void RegisterClientStartTransmitMessage(TcpSession<ushort> session)
    {
        session.OnReceive<ClientStartTransmitMessage>((_, tcpSession) =>
        {
            tcpSession.OnDataReceived += TcpSessionOnOnDataReceived;
            tcpSession.RedirectReceivedData = true;
        });
    }

    private void TcpSessionOnOnDataReceived(object? sender, ReceivedDataEventArgs e)
    {
        DoForwardDataToServer((TcpSession<ushort>)sender!, e.Data);
    }

    protected override void InvokeOnClientDisconnected(object sender, ClientConnectionChangedEventArgs<TcpSession<ushort>> e)
    {
    }
}