using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;

namespace Hive.Framework.Networking.Quic;

#pragma warning disable CA1416 

[RequiresPreviewFeatures]
public sealed class QuicSession<TId> : AbstractSession<TId, QuicSession<TId>> where TId : unmanaged
{
    public QuicSession(QuicConnection connection, QuicStream stream, IPacketCodec<TId> packetCodec, IDataDispatcher<QuicSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
    {
        QuicConnection = connection;
        QuicStream = stream;

        LocalEndPoint = connection.LocalEndPoint;
        RemoteEndPoint = connection.RemoteEndPoint;

        _connectionReady = true;
    }

    public QuicSession(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<QuicSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
    {
        if (!QuicListener.IsSupported)
            throw new NotSupportedException("QUIC is not supported on this platform!");

        Connect(endPoint);
    }

    public QuicSession(string addressWithPort, IPacketCodec<TId> packetCodec, IDataDispatcher<QuicSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
    {
        if (!QuicListener.IsSupported)
            throw new NotSupportedException("QUIC is not supported on this platform!");

        Connect(addressWithPort);
    }

    private bool _connectionReady;

    public QuicConnection? QuicConnection { get; private set; }
    public QuicStream? QuicStream { get; private set; }

    public override bool ShouldDestroyAfterDisconnected => true;
    public override bool CanSend => _connectionReady;
    public override bool CanReceive => _connectionReady;
    public override bool IsConnected => _connectionReady;

    protected override async ValueTask DispatchPacket(IPacketDecodeResult<object>? packet, Type? packetType = null)
    {
        if (packet == null) return;

        await DataDispatcher.DispatchAsync(this, packet, packetType);
    }

    public override async ValueTask DoConnect()
    {
        await DoDisconnect();
        await base.DoConnect();

        var clientConnectionOptions = new QuicClientConnectionOptions
        {
            RemoteEndPoint = RemoteEndPoint!,
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http2 },
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            },
            IdleTimeout = TimeSpan.FromMinutes(5)
        };

        QuicConnection = await QuicConnection.ConnectAsync(clientConnectionOptions);
        QuicStream = await QuicConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);

        _connectionReady = true;
    }

    public override async ValueTask SendOnce(ReadOnlyMemory<byte> data)
    {
        if (QuicStream == null)
            throw new InvalidOperationException("QuicStream Init failed!");

        if (!IsConnected || !CanSend) SpinWait.SpinUntil(() => IsConnected && CanReceive);

        await QuicStream.WriteAsync(data);
    }

    public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer)
    {
        if (QuicStream == null)
            throw new InvalidOperationException("QuicStream Init failed!");

        if (!IsConnected || !CanReceive || !QuicStream.CanRead)
            SpinWait.SpinUntil(() => IsConnected && CanReceive && QuicStream.CanRead);
        return await QuicStream.ReadAsync(buffer);
    }

    public override async ValueTask DoDisconnect()
    {
        await base.DoDisconnect();

        _connectionReady = false;

        if (QuicConnection != null)
        {
            await QuicConnection.DisposeAsync();
            QuicConnection = null;
        }

        if (QuicStream != null)
        {
            await QuicStream.DisposeAsync();
            QuicStream = null;
        }
    }
}