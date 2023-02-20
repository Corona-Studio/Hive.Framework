using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Networking.Tests.Messages;

namespace Hive.Framework.Networking.Tests.Tcp;

public class FakeTcpClientManager : AbstractClientManager<Guid, TcpSession<ushort>>
{
    protected override void RegisterHeartBeatMessage(TcpSession<ushort> session)
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

    protected override void RegisterSigninMessage(TcpSession<ushort> session)
    {
        session.OnReceive<SigninMessage>((message, tcpSession) =>
        {
            SigninMessageVal = message.Id;
            ConnectedClient++;
            OnClientConnected(tcpSession);
        });
    }

    protected override void RegisterClientSignOutMessage(TcpSession<ushort> session)
    {
        session.OnReceive<SignOutMessage>((message, tcpSession) =>
        {
            SignOutMessageVal = message.Id;
            ConnectedClient--;

            var sessionId = GetSessionId(tcpSession);

            OnClientDisconnected(sessionId, session, true);
        });
    }

    protected override void RegisterReconnectMessage(TcpSession<ushort> session)
    {

    }

    protected override void SendHeartBeat(TcpSession<ushort> session)
    {
        session.Send(new HeartBeatMessage());
    }

    protected override Guid CreateNewSessionId() => Guid.NewGuid();
}