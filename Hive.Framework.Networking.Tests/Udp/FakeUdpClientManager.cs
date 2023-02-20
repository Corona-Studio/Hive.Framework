﻿using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Networking.Udp;

namespace Hive.Framework.Networking.Tests.Udp;

public class FakeUdpClientManager : AbstractClientManager<Guid, UdpSession<ushort>>
{
    private readonly ConcurrentDictionary<IPEndPoint, UdpSession<ushort>> _endPointSessionMapper = new ();

    public override void AddSession(UdpSession<ushort> session)
    {
        if (session.RemoteEndPoint == null) return;
        if (!_endPointSessionMapper.TryGetValue(session.RemoteEndPoint, out var existSession))
        {
            _endPointSessionMapper.AddOrUpdate(session.RemoteEndPoint, session, (_, _) => session);
            base.AddSession(session);
            return;
        }

        {
            var writtenSpan = ((ArrayBufferWriter<byte>)session.DataWriter).WrittenSpan;

            existSession.DataWriter.Write(writtenSpan);
            existSession.AdvanceLengthCanRead(writtenSpan.Length);
        }
    }

    protected override void RegisterHeartBeatMessage(UdpSession<ushort> session)
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

    protected override void OnClientDisconnected(Guid sessionId, UdpSession<ushort> session, bool isClientRequest)
    {
        base.OnClientDisconnected(sessionId, session, isClientRequest);

        if (!isClientRequest)
            DisconnectedClient++;
    }

    protected override void RegisterSigninMessage(UdpSession<ushort> session)
    {
        session.OnReceive<SigninMessage>((message, tcpSession) =>
        {
            SigninMessageVal = message.Id;
            ConnectedClient++;
            OnClientConnected(tcpSession);
        });
    }

    protected override void RegisterClientSignOutMessage(UdpSession<ushort> session)
    {
        session.OnReceive<SignOutMessage>((message, tcpSession) =>
        {
            SignOutMessageVal = message.Id;
            ConnectedClient--;

            var sessionId = GetSessionId(tcpSession);

            OnClientDisconnected(sessionId, session, true);
        });
    }

    protected override void RegisterReconnectMessage(UdpSession<ushort> session)
    {
        session.OnReceive<ReconnectMessage>((_, tcpSession) =>
        {
            ReconnectedClient++;

            var sessionId = GetSessionId(tcpSession);

            OnClientReconnected(tcpSession, sessionId, true);
        });
    }

    protected override void SendHeartBeat(UdpSession<ushort> session)
    {
        session.Send(new HeartBeatMessage());
    }

    protected override Guid CreateNewSessionId() => Guid.NewGuid();
}