using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Networking.Tests.Messages.BidirectionalPacket;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Tcp;

public class FakeTcpClientManager : AbstractClientManager<Guid, TcpSession<ushort>>, INetworkingTestProperties
{
    protected override void RegisterHeartBeatMessage(TcpSession<ushort> session)
    {
        session.OnReceive<HeartBeatMessage>((_, tcpSession) =>
        {
            var sessionId = GetSessionId(tcpSession);

            UpdateHeartBeatReceiveTime(sessionId);
        });
    }

    public override int SessionIdSize => 16;

    public int ConnectedClient { get; private set; }
    public int SigninMessageVal { get; private set; }
    public int SignOutMessageVal { get; private set; }
    public int ReconnectedClient { get; private set; }
    public int DisconnectedClient { get; private set; }
    public int AdderCount { get; private set; }
    public int AdderPackageReceiveCount { get; private set; }
    public int BidirectionalPacketAddResult { get; private set; }

    public override ReadOnlyMemory<byte> GetEncodedC2SSessionPrefix(TcpSession<ushort> session)
    {
        return GetSessionId(session).ToByteArray();
    }

    public override Guid ResolveSessionPrefix(ReadOnlyMemory<byte> payload)
    {
        // [LENGTH (2) | PACKET_FLAGS (4) | PACKET_ID | SESSION_ID | PAYLOAD]
        const int startIndex = 2 + 4 + sizeof(ushort);
        var sessionIdMemory = payload.Slice(startIndex, 16);

        return new Guid(sessionIdMemory.Span);
    }

    protected override void InvokeOnClientDisconnected(Guid sessionId, TcpSession<ushort> session, bool isClientRequest)
    {
        base.InvokeOnClientDisconnected(sessionId, session, isClientRequest);

        if (!isClientRequest)
            DisconnectedClient++;
    }

    protected override void RegisterSigninMessage(TcpSession<ushort> session)
    {
        session.OnReceive<SigninMessage>((message, tcpSession) =>
        {
            SigninMessageVal = message.Payload.Id;
            ConnectedClient++;
            InvokeOnClientConnected(tcpSession);
        });

        session.OnReceive<CountTestMessage>((message, _) =>
        {
            AdderPackageReceiveCount++;
            AdderCount += message.Payload.Adder;
        });

        session.OnReceive<C2STestPacket>(async (message, tcpSession) =>
        {
            BidirectionalPacketAddResult += message.Payload.RandomNumber;
            await tcpSession.SendAsync(new S2CTestPacket { ReversedRandomNumber = -message.Payload.RandomNumber }, PacketFlags.None);
        });
    }

    protected override void RegisterClientSignOutMessage(TcpSession<ushort> session)
    {
        session.OnReceive<SignOutMessage>((message, tcpSession) =>
        {
            SignOutMessageVal = message.Payload.Id;
            ConnectedClient--;

            var sessionId = GetSessionId(tcpSession);

            InvokeOnClientDisconnected(sessionId, tcpSession, true);
        });
    }

    protected override void RegisterReconnectMessage(TcpSession<ushort> session)
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