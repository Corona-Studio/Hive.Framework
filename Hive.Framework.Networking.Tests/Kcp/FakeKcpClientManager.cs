﻿using Hive.Framework.Networking.Kcp;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tests.Messages;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Hive.Framework.Networking.Tests.Kcp;

public class FakeKcpClientManager : AbstractClientManager<Guid, KcpSession<ushort>>
{
    private readonly ConcurrentDictionary<IPEndPoint, KcpSession<ushort>> _endPointSessionMapper = new();

    public override void AddSession(KcpSession<ushort> session)
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

    public override ReadOnlyMemory<byte> GetEncodedSessionId(KcpSession<ushort> session)
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
            SigninMessageVal = message.Id;
            ConnectedClient++;
            InvokeOnClientConnected(kcpSession);
        });
    }

    protected override void RegisterClientSignOutMessage(KcpSession<ushort> session)
    {
        session.OnReceive<SignOutMessage>((message, kcpSession) =>
        {
            SignOutMessageVal = message.Id;
            ConnectedClient--;

            var sessionId = GetSessionId(kcpSession);

            InvokeOnClientDisconnected(sessionId, session, true);
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

    protected override void SendHeartBeat(KcpSession<ushort> session)
    {
        session.Send(new HeartBeatMessage());
    }

    protected override Guid CreateNewSessionId() => Guid.NewGuid();
}