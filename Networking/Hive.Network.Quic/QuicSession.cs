using System.Net;
using System.Net.Quic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hive.Network.Shared.Session;
using Microsoft.Extensions.Logging;

namespace Hive.Framework.Networking.Quic;

[RequiresPreviewFeatures]
[SupportedOSPlatform(nameof(OSPlatform.Windows))]
[SupportedOSPlatform(nameof(OSPlatform.Linux))]
[SupportedOSPlatform(nameof(OSPlatform.OSX))]
public sealed class QuicSession : AbstractSession
{
    public QuicSession(
        int sessionId,
        QuicConnection connection,
        QuicStream stream,
        ILogger<QuicSession> logger)
        : base(sessionId, logger)
    {
        QuicConnection = connection;
        QuicStream = stream;
    }

    public QuicConnection? QuicConnection { get; private set; }
    public QuicStream? QuicStream { get; private set; }

    public override IPEndPoint? LocalEndPoint => QuicConnection?.LocalEndPoint;
    public override IPEndPoint? RemoteEndPoint => QuicConnection?.RemoteEndPoint;

    public override bool CanSend => IsConnected;
    public override bool CanReceive => IsConnected;

    public event EventHandler<QuicError>? OnQuicError;

    public override async ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
    {
        if (QuicStream == null)
        {
            OnQuicError?.Invoke(this, QuicError.StreamAborted);
            return 0;
        }

        if (!IsConnected || !CanSend || !QuicStream.CanWrite)
            SpinWait.SpinUntil(() => IsConnected && CanReceive && QuicStream.CanWrite);

        await QuicStream.WriteAsync(data, token);

        return data.Count;
    }

    public override async ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
    {
        if (QuicStream == null)
        {
            OnQuicError?.Invoke(this, QuicError.ConnectionAborted);
            return 0;
        }

        if (!IsConnected || !CanReceive || !QuicStream.CanRead)
        {
            OnQuicError?.Invoke(this, QuicError.ConnectionIdle);
            return 0;
        }

        return await QuicStream.ReadAsync(buffer, token);
    }

    public override void Close()
    {
        IsConnected = false;

        if (QuicStream != null)
        {
            QuicStream.CompleteWrites();
            QuicStream.DisposeAsync().AsTask().Wait();
            QuicStream = null;
        }

        if (QuicConnection != null)
        {
            QuicConnection.DisposeAsync().AsTask().Wait();
            QuicConnection = null;
        }
    }
}