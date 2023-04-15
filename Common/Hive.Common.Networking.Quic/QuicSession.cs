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

    public QuicConnection? QuicConnection { get; private set; }
    public QuicStream? QuicStream { get; private set; }

    public override bool CanSend => true;
    public override bool CanReceive => true;
    public override bool IsConnected => true;

    protected override void DispatchPacket(object? packet, Type? packetType = null)
    {
        if (packet == null) return;

        DataDispatcher.Dispatch(this, packet, packetType);
    }

    public override async ValueTask DoConnect()
    {
        var clientConnectionOptions = new QuicClientConnectionOptions
        {
            RemoteEndPoint = RemoteEndPoint!,
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };

        QuicConnection = await QuicConnection.ConnectAsync(clientConnectionOptions);
        QuicStream = await QuicConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
    }

    public override async ValueTask DoDisconnect()
    {
        if(QuicStream != null)
            await QuicStream.DisposeAsync();

        if (QuicConnection != null)
        {
            await QuicConnection.CloseAsync(0x0C);
            await QuicConnection.DisposeAsync();
        }
    }

    public override async ValueTask SendOnce(ReadOnlyMemory<byte> data)
    {
        if (QuicStream == null)
            throw new InvalidOperationException("QuicStream Init failed!");

        await QuicStream.WriteAsync(data);
    }

    public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer)
    {
        if (QuicStream == null)
            throw new InvalidOperationException("QuicStream Init failed!");

        return await QuicStream.ReadAsync(buffer);
    }
}