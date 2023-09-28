using Hive.Framework.Networking.Shared;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System;
using System.Threading;
using Hive.Framework.Networking.Abstractions;
using Microsoft.Extensions.Logging;

namespace Hive.Framework.Networking.Udp
{
    /// <summary>
    /// 基于 Socket 的 UDP 传输层实现
    /// </summary>
    public sealed class UdpSession : AbstractSession
    {
        private UdpAcceptor _acceptor;


        public UdpSession(IPEndPoint remoteEndPoint,UdpAcceptor udpAcceptor,
            IMessageStreamPool messageStreamPool, ILogger<UdpSession> logger) : base(messageStreamPool, logger)
        {
            RemoteEndPoint = remoteEndPoint;
            _acceptor = udpAcceptor;
        }

        public Socket? Socket { get; private set; }

        public override IPEndPoint? LocalEndPoint { get; }

        public override IPEndPoint? RemoteEndPoint { get; }

        public override bool CanSend => true;

        public override bool CanReceive => true;

        public override bool IsConnected => Socket != null;

        private readonly byte[] _sendBuffer = new byte[DefaultBufferSize];

        private readonly byte[] _receiveBuffer = new byte[DefaultBufferSize];

        public override async ValueTask<int> SendOnce(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            data.CopyTo(_sendBuffer);
            return await Socket.SendToAsync(new ArraySegment<byte>(_sendBuffer,0, data.Length), SocketFlags.None, RemoteEndPoint);
        }

        public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer, CancellationToken token)
        {
            var result = await Socket.ReceiveFromAsync(new ArraySegment<byte>(_receiveBuffer,0,DefaultSocketBufferSize),
                SocketFlags.None, RemoteEndPoint);

            return result.ReceivedBytes;
        }
    }
}