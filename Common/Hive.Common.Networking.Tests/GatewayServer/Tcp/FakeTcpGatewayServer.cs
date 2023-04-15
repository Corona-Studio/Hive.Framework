using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Abstractions.EventArgs;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Networking.Tests.Messages;

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
            foreach (var packetId in message.PackagesToReceive)
            {
                AddPacketRoute(packetId, tcpSession);
                RegisteredForwardPacketCount++;
            }
        });
    }

    protected override void NotifyClientCanStartTransmitMessage(TcpSession<ushort> session)
    {
        session.Send(new ClientCanTransmitMessage());
    }

    protected override void RegisterClientStartTransmitMessage(TcpSession<ushort> session)
    {
        session.OnReceive<ClientStartTransmitMessage>((message, tcpSession) =>
        {
            tcpSession.ExcludeRedirectPacketIds = message.ExcludeRedirectPacketIds;
            tcpSession.OnDataReceived += TcpSessionOnOnDataReceived;
            tcpSession.RedirectReceivedData = true;

            NotifyClientCanStartTransmitMessage(session);
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