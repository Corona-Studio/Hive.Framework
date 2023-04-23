using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System;
using System.Collections.Concurrent;

namespace Hive.Framework.Networking.Udp
{
    /// <summary>
    /// 基于 Socket 的 UDP 传输层实现
    /// </summary>
    public sealed class UdpSession<TId> : AbstractSession<TId, UdpSession<TId>> where TId : unmanaged
    {
        public UdpSession(
            UdpClient socket,
            IPEndPoint endPoint,
            IPacketCodec<TId> packetCodec,
            IDataDispatcher<UdpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            UdpConnection = socket;

            RemoteEndPoint = endPoint;
        }

        public UdpSession(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<UdpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Connect(endPoint);
        }

        public UdpSession(string addressWithPort, IPacketCodec<TId> packetCodec, IDataDispatcher<UdpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Connect(addressWithPort);
        }

        private bool _closed;

        public UdpClient? UdpConnection { get; private set; }
        public ConcurrentQueue<byte[]> DataQueue { get; } = new ConcurrentQueue<byte[]>();

        public override bool CanSend => true;
        public override bool CanReceive => true;
        public override bool IsConnected => UdpConnection?.Client != null;

        protected override void DispatchPacket(object? packet, Type? packetType = null)
        {
            if (packet == null) return;

            DataDispatcher.Dispatch(this, packet, packetType);
        }

        public override async ValueTask DoConnect()
        {
            // 释放先前的连接
            await DoDisconnect();

            // 创建新连接
            _closed = false;
            UdpConnection = new UdpClient();
        }

        public override ValueTask DoDisconnect()
        {
            if (_closed || UdpConnection == null) return default;

            UdpConnection.Dispose();
            _closed = true;

            return default;
        }

        public override async ValueTask SendOnce(ReadOnlyMemory<byte> data)
        {
            if (UdpConnection == null)
                throw new InvalidOperationException("Socket Init failed!");

            var totalLen = data.Length;
            var sentLen = 0;

            while (sentLen < totalLen)
            {
                var sendThisTime = await UdpConnection.SendAsync(data[sentLen..].ToArray(), data.Length - sentLen, RemoteEndPoint);
                sentLen += sendThisTime;
            }
        }
        
        public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer)
        {
            if (UdpConnection == null)
                throw new InvalidOperationException("Socket Init failed!");

            byte[]? data;

            while (!DataQueue.TryDequeue(out data))
            {
                await Task.Delay(10);
            }

            if (data == null || data.Length == 0)
                return 0;

            data.AsSpan().CopyTo(buffer.Span);

            return data.Length;
        }

        public override void Dispose()
        {
            base.Dispose();
            DoDisconnect();
        }
    }
}