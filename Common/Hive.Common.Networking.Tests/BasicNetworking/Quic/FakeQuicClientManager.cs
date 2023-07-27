using System.Runtime.Versioning;
using System.Text;
using Hive.Framework.Networking.Quic;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Networking.Tests.Messages.BidirectionalPacket;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Quic;

[RequiresPreviewFeatures]
public class FakeQuicClientManager : AbstractClientManager<Guid, QuicSession<ushort>>, INetworkingTestProperties
{
    protected override void RegisterHeartBeatMessage(QuicSession<ushort> session)
    {
        session.OnReceive<HeartBeatMessage>((_, quicSession) =>
        {
            var sessionId = GetSessionId(quicSession);

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

    public override ReadOnlyMemory<byte> GetEncodedSessionPrefix(QuicSession<ushort> session)
    {
        return Encoding.ASCII.GetBytes(GetSessionId(session).ToString("N"));
    }

    protected override void InvokeOnClientDisconnected(Guid sessionId, QuicSession<ushort> session, bool isClientRequest)
    {
        base.InvokeOnClientDisconnected(sessionId, session, isClientRequest);

        if (!isClientRequest)
            DisconnectedClient++;
    }

    protected override void RegisterSigninMessage(QuicSession<ushort> session)
    {
        session.OnReceive<SigninMessage>((message, quicSession) =>
        {
            SigninMessageVal = message.Id;
            ConnectedClient++;
            InvokeOnClientConnected(quicSession);
        });

        session.OnReceive<CountTestMessage>((message, _) =>
        {
            AdderPackageReceiveCount++;
            AdderCount += message.Adder;
        });

        session.OnReceive<C2STestPacket>(async (message, quicSession) =>
        {
            BidirectionalPacketAddResult += message.RandomNumber;
            await quicSession.SendAsync(new S2CTestPacket { ReversedRandomNumber = -message.RandomNumber });
        });
    }

    protected override void RegisterClientSignOutMessage(QuicSession<ushort> session)
    {
        session.OnReceive<SignOutMessage>((message, tcpSession) =>
        {
            SignOutMessageVal = message.Id;
            ConnectedClient--;

            var sessionId = GetSessionId(tcpSession);

            InvokeOnClientDisconnected(sessionId, tcpSession, true);
        });
    }

    protected override void RegisterReconnectMessage(QuicSession<ushort> session)
    {
        session.OnReceive<ReconnectMessage>((_, tcpSession) =>
        {
            ReconnectedClient++;

            var sessionId = GetSessionId(tcpSession);

            InvokeOnClientReconnected(tcpSession, sessionId, true);
        });
    }

    protected override Guid CreateNewSessionId() => Guid.NewGuid();
}