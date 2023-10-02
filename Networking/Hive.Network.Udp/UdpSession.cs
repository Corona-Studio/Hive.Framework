using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions;
using Hive.Network.Shared.Session;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Udp
{
    /// <summary>
    /// 基于 Socket 的 UDP 传输层实现
    /// </summary>
    public class UdpSession : AbstractSession
    {
        public UdpSession(int sessionId, IPEndPoint remoteEndPoint, IPEndPoint localEndPoint, ILogger<UdpSession> logger, IMessageBufferPool messageBufferPool) : base(sessionId, logger, messageBufferPool)
        {
            LocalEndPoint = localEndPoint;
            RemoteEndPoint = remoteEndPoint;
        }

        public override IPEndPoint LocalEndPoint { get; }

        public override IPEndPoint RemoteEndPoint { get; }

        public override bool CanSend => IsConnected;

        public override bool CanReceive => IsConnected;

        public override bool IsConnected => _isConnected;
        public override ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
        {
            throw new NotImplementedException();
        }


        private bool _isConnected = true;
        
        public override void Close()
        {
            _isConnected = false;
        }
    }
}