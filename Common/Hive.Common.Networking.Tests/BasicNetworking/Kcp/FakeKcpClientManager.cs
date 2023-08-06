using Hive.Framework.Networking.Kcp;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Networking.Tests.Messages.BidirectionalPacket;
using Hive.Framework.Shared;
using System.Text;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Kcp;

public class FakeKcpClientManager : AbstractClientManager<Guid, KcpSession<ushort>>, INetworkingTestProperties
{
    protected override void RegisterHeartBeatMessage(KcpSession<ushort> session)
    {
        session.OnReceive<HeartBeatMessage>((_, kcpSession) =>
        {
            var sessionId = GetSessionId(kcpSession);

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

    public override ReadOnlyMemory<byte> GetEncodedC2SSessionPrefix(KcpSession<ushort> session)
    {
        return Encoding.ASCII.GetBytes(GetSessionId(session).ToString("N"));
    }

    protected override void InvokeOnClientDisconnected(Guid sessionId, KcpSession<ushort> session, bool isClientRequest)
    {
        base.InvokeOnClientDisconnected(sessionId, session, isClientRequest);

        if (!isClientRequest)
            DisconnectedClient++;
    }

    protected override void RegisterSigninMessage(KcpSession<ushort> session)
    {
        session.OnReceive<SigninMessage>((message, kcpSession) =>
        {
            SigninMessageVal = message.Payload.Id;
            ConnectedClient++;
            InvokeOnClientConnected(kcpSession);
        });

        session.OnReceive<CountTestMessage>((message, _) =>
        {
            AdderPackageReceiveCount++;
            AdderCount += message.Payload.Adder;
        });

        session.OnReceive<C2STestPacket>(async (message, kcpSession) =>
        {
            BidirectionalPacketAddResult += message.Payload.RandomNumber;
            await kcpSession.SendAsync(new S2CTestPacket { ReversedRandomNumber = -message.Payload.RandomNumber }, PacketFlags.None);
        });
    }

    protected override void RegisterClientSignOutMessage(KcpSession<ushort> session)
    {
        session.OnReceive<SignOutMessage>((message, kcpSession) =>
        {
            SignOutMessageVal = message.Payload.Id;
            ConnectedClient--;

            var sessionId = GetSessionId(kcpSession);

            InvokeOnClientDisconnected(sessionId, kcpSession, true);
        });
    }

    protected override void RegisterReconnectMessage(KcpSession<ushort> session)
    {
        session.OnReceive<ReconnectMessage>((_, kcpSession) =>
        {
            ReconnectedClient++;

            var sessionId = GetSessionId(kcpSession);

            InvokeOnClientReconnected(kcpSession, sessionId, true);
        });
    }

    protected override Guid CreateNewSessionId() => Guid.NewGuid();
}