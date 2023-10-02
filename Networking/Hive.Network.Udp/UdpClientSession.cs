using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Udp
{
    /// <summary>
    /// 基于 Socket 的 UDP 传输层实现
    /// </summary>
    public class UdpClientSession : UdpSession
    {
        private Socket? _socket;
        public UdpClientSession(int sessionId, Socket socket ,IPEndPoint remoteEndPoint, ILogger<UdpClientSession> logger, IMessageBufferPool messageBufferPool) 
            : base(sessionId, remoteEndPoint, (IPEndPoint)socket.LocalEndPoint, logger, messageBufferPool)
        {
            _socket = socket;
        }

        public override bool IsConnected => _isConnected;
        

        public override async ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
        {
            if (!IsConnected)
                return 0;
            
            return await _socket.SendToAsync(data, SocketFlags.None, RemoteEndPoint);
        }

        private bool _isConnected = true;

        public override async ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
        {
            if (!_isConnected) return 0;

            var received = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, RemoteEndPoint);
            return received.ReceivedBytes;
        }
        
        public override void Close()
        {
            _isConnected = false;
            _socket?.Close();
            _socket?.Dispose();
            _socket = null;
        }
    }
}