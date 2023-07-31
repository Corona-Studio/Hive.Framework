using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System;
using System.Threading.Channels;
using Hive.Framework.Networking.Shared.Helpers;
using System.Buffers;

namespace Hive.Framework.Networking.Udp
{
    /// <summary>
    /// 基于 Socket 的 UDP 传输层实现
    /// </summary>
    public sealed class UdpSession<TId> : AbstractSession<TId, UdpSession<TId>> where TId : unmanaged
    {
        public UdpSession(
            Socket socket,
            IPEndPoint endPoint,
            IPacketCodec<TId> packetCodec,
            IDataDispatcher<UdpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Socket = socket;
            //socket.PatchSocket();
            socket.ReceiveBufferSize = DefaultSocketBufferSize;

            _passiveMode = true;

            LocalEndPoint = socket.LocalEndPoint as IPEndPoint;
            RemoteEndPoint = endPoint;
        }

        public UdpSession(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<UdpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            _passiveMode = false;
            Connect(endPoint);
        }

        public UdpSession(string addressWithPort, IPacketCodec<TId> packetCodec, IDataDispatcher<UdpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            _passiveMode = false;
            Connect(addressWithPort);
        }

        private bool _closed;

        // 指示是否为被动模式，如果是被动模式，则所有数据将通过 Acceptor 接受后注入 KCP
        // 如果为非被动模式，则使用内部的 Receive 方法从 Socket 接收数据
        private readonly bool _passiveMode;

        public Socket? Socket { get; private set; }
        public Channel<ReadOnlyMemory<byte>> DataChannel { get; } = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(1000)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        public override bool ShouldDestroyAfterDisconnected => false;
        public override bool CanSend => true;
        public override bool CanReceive => true;
        public override bool IsConnected => Socket != null;

        protected override async ValueTask DispatchPacket(IPacketDecodeResult<object>? packet, Type? packetType = null)
        {
            if (packet == null) return;

            await DataDispatcher.DispatchAsync(this, packet, packetType);
        }

        public override async ValueTask DoConnect()
        {
            // 释放先前的连接
            await DoDisconnect();
            await base.DoConnect();

            // 创建新连接
            _closed = false;
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket.ReceiveBufferSize = DefaultSocketBufferSize;
            // Socket.PatchSocket();

            TaskHelper.ManagedRun(NonPassiveModeRawReceiveLoop, CancellationTokenSource!.Token);
        }

        public override ValueTask DoDisconnect()
        {
            if (_closed || Socket == null) return default;

            Socket.Dispose();
            _closed = true;

            return default;
        }

        public override ValueTask SendOnce(ReadOnlyMemory<byte> data)
        {
            if (Socket == null)
                throw new InvalidOperationException("Socket Init failed!");

            var totalLen = data.Length;
            var sentLen = 0;

            while (sentLen < totalLen)
            {
                var sendThisTime = Socket.SendTo(data[sentLen..].ToArray(), RemoteEndPoint!);
                sentLen += sendThisTime;
            }

            return default;
        }

        private async Task NonPassiveModeRawReceiveLoop()
        {
            if (Socket == null)
                throw new NullReferenceException(nameof(Socket));
            if (CancellationTokenSource == null)
                throw new ArgumentNullException(nameof(CancellationTokenSource));
            if (_passiveMode)
                throw new InvalidOperationException("该方法仅支持在非被动模式下启用！");

            while (!(CancellationTokenSource?.IsCancellationRequested ?? true))
            {
                if (Socket!.Available <= 0)
                {
                    await Task.Delay(1);
                    continue;
                }

                var buffer = ArrayPool<byte>.Shared.Rent(1024);
                try
                {
                    EndPoint? endPoint = new IPEndPoint(IPAddress.Any, 0);
                    var received = Socket.ReceiveFrom(buffer, ref endPoint);

                    if (received == 0 || !endPoint.Equals(RemoteEndPoint))
                    {
                        await Task.Delay(10);
                        continue;
                    }

                    await DataChannel.Writer.WriteAsync(buffer[..received], CancellationTokenSource.Token);
                    await Task.Delay(10);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer)
        {
            if (Socket == null)
                throw new InvalidOperationException("Socket Init failed!");
            if (CancellationTokenSource == null)
                throw new ArgumentNullException(nameof(CancellationTokenSource));

            if (!await DataChannel.Reader.WaitToReadAsync())
                return 0;

            var data = await DataChannel.Reader.ReadAsync(CancellationTokenSource.Token);

            if (data.Length == 0 || data.Length > buffer.Length)
                return 0;

            data.CopyTo(buffer);

            return data.Length;
        }
    }
}