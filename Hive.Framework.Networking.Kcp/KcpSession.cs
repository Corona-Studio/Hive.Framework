using System;
using System.Net.Sockets;
using System.Net;
using System.Net.Sockets.Kcp;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using System.Buffers;
using System.Threading;

namespace Hive.Framework.Networking.Kcp
{
    public sealed class KcpSession<TId> : AbstractSession<TId, KcpSession<TId>>, IKcpCallback where TId : unmanaged
    {
        public KcpSession(Socket socket, IPacketCodec<TId> packetCodec, IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Socket = socket;
            socket.ReceiveBufferSize = 8192 * 4;

            _kcp = new PoolSegManager.Kcp(2001, this);

            LocalEndPoint = socket.LocalEndPoint as IPEndPoint;
            RemoteEndPoint = socket.RemoteEndPoint as IPEndPoint;
        }

        public KcpSession(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Connect(endPoint);

            _kcp = new PoolSegManager.Kcp(2001, this);
        }

        public KcpSession(string addressWithPort, IPacketCodec<TId> packetCodec, IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Connect(addressWithPort);

            _kcp = new PoolSegManager.Kcp(2001, this);
        }

        private bool _closed;
        private readonly PoolSegManager.Kcp _kcp;

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

        /// <summary>
        /// KCP 发送方法，
        /// 将数据发送至 KCP 库加以处理并排序
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public override async ValueTask SendOnce(ReadOnlyMemory<byte> data)
        {
            if (Socket == null)
                throw new InvalidOperationException("Socket Init failed!");

            _kcp.Send(data.Span);
        }

        public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer)
        {
            if (Socket == null)
                throw new InvalidOperationException("Socket Init failed!");

            Socket.Blocking = true;

            var length = await Socket.ReceiveAsync(buffer, SocketFlags.None);

            // 将读入的数据写入 KCP 分段管理器，来拼凑完整的数据包
            _kcp.Input(buffer.Span);

            return length;
        }

        /// <summary>
        /// 重写后的接收方法，
        /// 通过持续调用 KCP 库的接收方法来尝试接收数据
        /// </summary>
        /// <returns></returns>
        protected override async Task ReceiveLoop()
        {
            using var bufferOwner = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
            var buffer = bufferOwner.Memory;

            var receivedLen = 0; //当前共接受了多少数据
            var offset = 0; //当前接受到的数据在buffer中的偏移量，buffer中的有效数据：buffer[offset..offset+receivedLen]

            try
            {
                while (!CancellationTokenSource.IsCancellationRequested)
                {
                    if (!IsConnected || !CanReceive) SpinWait.SpinUntil(() => IsConnected && CanReceive);

                    var isInnerReceivedFailed = false;
                    var (received, avalidLength) = _kcp.TryRecv();

                    while (received == null)
                    {
                        var lenThisTime = await ReceiveOnce(buffer[(offset + receivedLen)..]);

                        if (lenThisTime == 0)
                        {
                            isInnerReceivedFailed = true;
                            break;
                        }

                        receivedLen += lenThisTime;
                        (received, avalidLength) = _kcp.TryRecv();

                        await Task.Delay(10);
                    }

                    if (isInnerReceivedFailed) break;

                    ProcessPacket(buffer[..avalidLength]);

                    offset = 0;
                    receivedLen = 0;
                }
            }
            finally
            {
                ReceivingLoopRunning = false;
            }
        }

        /// <summary>
        /// KCP 库发送实现，
        /// 在 KCP 完成封包处理后，通过该方法发送
        /// </summary>
        /// <param name="buffer">处理后的数据</param>
        /// <param name="avalidLength">有效长度</param>
        /// <exception cref="InvalidOperationException">Socket 初始化失败时抛出</exception>
        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            if (Socket == null)
                throw new InvalidOperationException("Socket Init failed!");

            var data = buffer.Memory.Span[..avalidLength];
            var totalLen = data.Length;
            var sentLen = 0;

            Socket.Blocking = true;

            while (sentLen < totalLen)
            {
                var sendThisTime = Socket.Send(data[sentLen..], SocketFlags.None);

                sentLen += sendThisTime;
            }
            
            buffer.Dispose();
        }

        public override void Dispose()
        {
            base.Dispose();
            DoDisconnect();
            _kcp.Dispose();
        }
    }
}