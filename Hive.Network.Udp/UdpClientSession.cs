using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Udp
{
    /// <summary>
    ///     基于 Socket 的 UDP 传输层实现
    /// </summary>
    public sealed class UdpClientSession : UdpSession
    {
        private Socket? _socket;

        public UdpClientSession(
            int sessionId,
            Socket socket,
            IPEndPoint remoteEndPoint,
            ILogger<UdpClientSession> logger)
            : base(sessionId, remoteEndPoint, (IPEndPoint)socket.LocalEndPoint, logger)
        {
            _socket = socket;
        }

        public override async ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
        {
            if (token.IsCancellationRequested || !IsConnected)
                return 0;

            try
            {
                return await _socket.SendToAsync(data, SocketFlags.None, RemoteEndPoint);
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
            if (token.IsCancellationRequested || !IsConnected)
                return 0;

            try
            {
                var received = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, RemoteEndPoint);
                return received.ReceivedBytes;
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
            _socket?.Close();
            _socket?.Dispose();
            _socket = null;
        }
    }
}