﻿using System;
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
public sealed class TcpSession : AbstractSession
{
    public TcpSession(
        int sessionId,
        Socket socket,
        ILogger<TcpSession> logger)
        : base(sessionId, logger)
    {
        Socket = socket;
        socket.ReceiveBufferSize = NetworkSettings.DefaultSocketBufferSize;
    }

    public Socket? Socket { get; private set; }

    public override IPEndPoint? LocalEndPoint => Socket?.LocalEndPoint as IPEndPoint;

    public override IPEndPoint? RemoteEndPoint => Socket?.RemoteEndPoint as IPEndPoint;

    public override bool CanSend => IsConnected && SendingLoopRunning;

    public override bool CanReceive => IsConnected && ReceivingLoopRunning;

    public override bool IsConnected => Socket is { Connected: true };

    public event EventHandler<SocketError>? OnSocketError;

    public override async ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
    {
        try
        {
            var len = await Socket.SendAsync(data, SocketFlags.None, token);

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

    public override async ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
    {
        try
        {
            return await Socket.ReceiveAsync(buffer, SocketFlags.None, token);
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