﻿using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
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

    public override ReadOnlyMemory<byte> GetEncodedSessionId(UdpSession<ushort> session)
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
            SigninMessageVal = message.Id;
            ConnectedClient++;
            InvokeOnClientConnected(udpSession);
        });
    }

    protected override void RegisterClientSignOutMessage(UdpSession<ushort> session)
    {
        session.OnReceive<SignOutMessage>((message, udpSession) =>
        {
            SignOutMessageVal = message.Id;
            ConnectedClient--;

            var sessionId = GetSessionId(udpSession);

            InvokeOnClientDisconnected(sessionId, session, true);
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

    protected override void SendHeartBeat(UdpSession<ushort> session)
    {
        session.Send(new HeartBeatMessage());
    }

    protected override Guid CreateNewSessionId() => Guid.NewGuid();
}