using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Tcp
{
    /// <summary>
    /// 基于 Socket 的 TCP 传输层实现
    /// </summary>
    public sealed class TcpSession<TId> : AbstractSession<TId, TcpSession<TId>> where TId : unmanaged
    {
        public TcpSession(Socket socket, IPacketCodec<TId> packetCodec, IDataDispatcher<TcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Socket = socket;
            socket.ReceiveBufferSize = DefaultSocketBufferSize;

            LocalEndPoint = socket.LocalEndPoint as IPEndPoint;
            RemoteEndPoint = socket.RemoteEndPoint as IPEndPoint;

            _connectionReady = true;
        }

        public TcpSession(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<TcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Connect(endPoint);
        }

        public TcpSession(string addressWithPort, IPacketCodec<TId> packetCodec, IDataDispatcher<TcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Connect(addressWithPort);
        }

        private bool _closed;
        private bool _connectionReady;

        public Socket? Socket { get; private set; }

        public override bool ShouldDestroyAfterDisconnected => true;
        public override bool CanSend => _connectionReady;
        public override bool CanReceive => _connectionReady;
        public override bool IsConnected => Socket is { Connected: true } && _connectionReady;

        protected override async ValueTask DispatchPacket(PacketDecodeResult<object?> packet, Type? packetType = null)
        {
            await DataDispatcher.DispatchAsync(this, packet, packetType);
        }

        public override async ValueTask DoConnect()
        {
            // 释放先前的连接
            await DoDisconnect();
            await base.DoConnect();

            // 创建新连接
            Socket?.Shutdown(SocketShutdown.Both);
            Socket?.Dispose();

            _closed = false;

            if (RemoteEndPoint == null)
                throw new ArgumentNullException(nameof(RemoteEndPoint));

            Socket = new Socket(RemoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // 连接到指定地址
            await Socket.ConnectAsync(RemoteEndPoint);

            _connectionReady = true;
        }

        public override ValueTask DoDisconnect()
        {
            base.DoDisconnect();
            _connectionReady = false;

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
                var sendThisTime = await Socket.SendAsync(data[sentLen..], SocketFlags.None);

                sentLen += sendThisTime;
            }
        }

        public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer)
        {
            if (Socket == null)
                throw new InvalidOperationException("Socket Init failed!");
            
            return await Socket.ReceiveAsync(buffer, SocketFlags.None);
        }
    }
}