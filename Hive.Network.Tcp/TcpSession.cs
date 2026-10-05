using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Hive.Network.Abstractions.Session;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Shared;
using Hive.Network.Shared.Session;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Tcp;

/// <summary>
///     基于 Socket 的 TCP 传输层实现
/// </summary>
public sealed class TcpSession : AbstractSession, IWritableFrameSession
{
    public TcpSession(
        int sessionId,
        Socket socket,
        ILogger<TcpSession> logger)
        : base(sessionId, logger)
    {
        Socket = socket;
        _localEndPoint = socket.LocalEndPoint as IPEndPoint;
        _remoteEndPoint = socket.RemoteEndPoint as IPEndPoint;
        socket.ReceiveBufferSize = NetworkSettings.DefaultSocketBufferSize;
    }

    private readonly IPEndPoint? _localEndPoint;
    private readonly IPEndPoint? _remoteEndPoint;
    public Socket? Socket { get; private set; }

    public override IPEndPoint? LocalEndPoint => Socket == null ? null : _localEndPoint;

    public override IPEndPoint? RemoteEndPoint => Socket == null ? null : _remoteEndPoint;

    public override bool CanSend => IsConnected && SendingLoopRunning;

    public override bool CanReceive => IsConnected && ReceivingLoopRunning;

    public override bool IsConnected => Socket is { Connected: true };

    public event EventHandler<SocketError>? OnSocketError;

    public ValueTask<bool> TrySendFrameAsync(Memory<byte> frame, CancellationToken token = default)
        => SendWritableFrameAsync(frame, token);

    public ValueTask<bool> TrySendAsync(ReadOnlySequence<byte> payload, CancellationToken token = default)
        => SendBorrowedSequenceAsync(payload, token);

#if NET10_0_OR_GREATER
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
#endif
    public override async ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
    {
        var socket = Socket;
        if (socket is null || token.IsCancellationRequested)
            return 0;

        try
        {
            var len = await socket.SendAsync(data, SocketFlags.None, token);

            if (len == 0)
                OnSocketError?.Invoke(this, SocketError.ConnectionReset);

            return len;
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode == SocketError.OperationAborted)
                return 0;

            throw;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
    }

#if NET10_0_OR_GREATER
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
#endif
    public override async ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
    {
        var socket = Socket;
        if (socket is null || token.IsCancellationRequested)
            return 0;

        try
        {
            return await socket.ReceiveAsync(buffer, SocketFlags.None, token);
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode == SocketError.OperationAborted)
                return 0;

            throw;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
    }

    public override void Close()
    {
        base.Close();

        IsConnected = false;
        Socket?.Close();
        Socket?.Dispose();
        Socket = null;
    }
}