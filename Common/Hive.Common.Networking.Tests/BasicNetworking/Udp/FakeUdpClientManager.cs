using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Networking.Udp;
using System.Text;
using Hive.Framework.Networking.Tests.Messages.BidirectionalPacket;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Udp;

public class FakeUdpClientManager : AbstractClientManager<Guid, UdpSession<ushort>>, INetworkingTestProperties
{
    protected override void RegisterHeartBeatMessage(UdpSession<ushort> session)
    {
        session.OnReceive<HeartBeatMessage>((_, udpSession) =>
        {
            var sessionId = GetSessionId(udpSession);

            UpdateHeartBeatReceiveTime(sessionId);
        });
    }

    public int ConnectedClient { get; private set; }
    public int SigninMessageVal { get; private set; }
    public int SignOutMessageVal { get; private set; }
    public int ReconnectedClient { get; private set; }
    public int DisconnectedClient { get; private set; }
    public int AdderCount { get; private set; }
    public int AdderPackageReceiveCount { get; private set; }
    public int BidirectionalPacketAddResult { get; private set; }

    public override ReadOnlyMemory<byte> GetEncodedC2SSessionPrefix(UdpSession<ushort> session)
    {
        return Encoding.ASCII.GetBytes(GetSessionId(session).ToString("N"));
    }

    protected override void InvokeOnClientDisconnected(Guid sessionId, UdpSession<ushort> session, bool isClientRequest)
    {
        base.InvokeOnClientDisconnected(sessionId, session, isClientRequest);

        if (!isClientRequest)
            DisconnectedClient++;
    }

    protected override void RegisterSigninMessage(UdpSession<ushort> session)
    {
        session.OnReceive<SigninMessage>((message, udpSession) =>
        {
            SigninMessageVal = message.Payload.Id;
            ConnectedClient++;
            InvokeOnClientConnected(udpSession);
        });

        session.OnReceive<CountTestMessage>((message, _) =>
        {
            AdderPackageReceiveCount++;
            AdderCount += message.Payload.Adder;
        });

        session.OnReceive<C2STestPacket>(async (message, udpSession) =>
        {
            BidirectionalPacketAddResult += message.Payload.RandomNumber;
            await udpSession.SendAsync(new S2CTestPacket { ReversedRandomNumber = -message.Payload.RandomNumber });
        });
    }

    protected override void RegisterClientSignOutMessage(UdpSession<ushort> session)
    {
        session.OnReceive<SignOutMessage>((message, udpSession) =>
        {
            SignOutMessageVal = message.Payload.Id;
            ConnectedClient--;

            var sessionId = GetSessionId(udpSession);

            InvokeOnClientDisconnected(sessionId, udpSession, true);
        });
    }

    protected override void RegisterReconnectMessage(UdpSession<ushort> session)
    {
        session.OnReceive<ReconnectMessage>((_, udpSession) =>
        {
            ReconnectedClient++;

            var sessionId = GetSessionId(udpSession);

            InvokeOnClientReconnected(udpSession, sessionId, true);
        });
    }

    protected override Guid CreateNewSessionId() => Guid.NewGuid();
}