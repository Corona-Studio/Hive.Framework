using System.Runtime.Versioning;
using Hive.Framework.Networking.Quic;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tests.Messages;

namespace Hive.Framework.Networking.Tests.Quic;

[RequiresPreviewFeatures]
public class FakeQuicClientManager : AbstractClientManager<Guid, QuicSession<ushort>>
{
    protected override void RegisterHeartBeatMessage(QuicSession<ushort> session)
    {
        session.OnReceive<HeartBeatMessage>((_, tcpSession) =>
        {
            var sessionId = GetSessionId(tcpSession);

            UpdateHeartBeatReceiveTime(sessionId);
        });
    }

    public int ConnectedClient { get; private set; }
    public int SigninMessageVal { get; private set; }
    public int SignOutMessageVal { get; private set; }
    public int ReconnectedClient { get; private set; }
    public int DisconnectedClient { get; private set; }

    protected override void OnClientDisconnected(Guid sessionId, QuicSession<ushort> session, bool isClientRequest)
    {
        base.OnClientDisconnected(sessionId, session, isClientRequest);

        if (!isClientRequest)
            DisconnectedClient++;
    }

    protected override void RegisterSigninMessage(QuicSession<ushort> session)
    {
        session.OnReceive<SigninMessage>((message, tcpSession) =>
        {
            SigninMessageVal = message.Id;
            ConnectedClient++;
            OnClientConnected(tcpSession);
        });
    }

    protected override void RegisterClientSignOutMessage(QuicSession<ushort> session)
    {
        session.OnReceive<SignOutMessage>((message, tcpSession) =>
        {
            SignOutMessageVal = message.Id;
            ConnectedClient--;

            var sessionId = GetSessionId(tcpSession);

            OnClientDisconnected(sessionId, session, true);
        });
    }

    protected override void RegisterReconnectMessage(QuicSession<ushort> session)
    {
        session.OnReceive<ReconnectMessage>((_, tcpSession) =>
        {
            ReconnectedClient++;

            var sessionId = GetSessionId(tcpSession);

            OnClientReconnected(tcpSession, sessionId, true);
        });
    }

    protected override void SendHeartBeat(QuicSession<ushort> session)
    {
        session.Send(new HeartBeatMessage());
    }

    protected override Guid CreateNewSessionId() => Guid.NewGuid();
}