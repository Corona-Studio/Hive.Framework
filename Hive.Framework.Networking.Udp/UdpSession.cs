using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System;

namespace Hive.Framework.Networking.Udp
{
    /// <summary>
    /// 基于 Socket 的 UDP 传输层实现
    /// </summary>
    public sealed class UdpSession<TId> : AbstractSession<TId, UdpSession<TId>>
    {
        public UdpSession(Socket socket, IPacketCodec<TId> packetCodec, IDataDispatcher<UdpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Socket = socket;
            socket.ReceiveBufferSize = 8192 * 4;

            LocalEndPoint = socket.LocalEndPoint as IPEndPoint;
            RemoteEndPoint = socket.RemoteEndPoint as IPEndPoint;
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

        public Socket? Socket { get; private set; }

        public override bool CanSend => true;
        public override bool CanReceive => true;
        public override bool IsConnected => Socket is { Connected: true };

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
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Udp);
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // 连接到指定地址
            await Socket.ConnectAsync(RemoteEndPoint);
        }

        public override ValueTask DoDisconnect()
        {
            if (_closed || Socket == null) return default;

            Socket?.Shutdown(SocketShutdown.Both);
            Socket?.Close();
            Socket = null;
            _closed = true;

            return default;
        }

        public override async ValueTask SendOnce(ReadOnlyMemory<byte> data)
        {
            if (Socket == null)
                throw new InvalidOperationException("Socket Init failed!");

            var totalLen = data.Length;
            var sentLen = 0;

            while (sentLen < totalLen)
            {
                Socket.Blocking = true;
                var sendThisTime = await Socket.SendAsync(data[sentLen..], SocketFlags.None);

                sentLen += sendThisTime;
            }
        }

        public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer)
        {
            if (Socket == null)
                throw new InvalidOperationException("Socket Init failed!");

            Socket.Blocking = true;

            return await Socket.ReceiveAsync(buffer, SocketFlags.None);
        }

        public override void Dispose()
        {
            base.Dispose();
            DoDisconnect();
        }
    }
}