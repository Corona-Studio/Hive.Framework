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
using Hive.Framework.Networking.Shared.Helpers;
using System.Threading.Channels;

namespace Hive.Framework.Networking.Kcp
{
    public sealed class KcpSession<TId> : AbstractSession<TId, KcpSession<TId>>, IKcpCallback where TId : unmanaged
    {
        public KcpSession(
            Socket socket,
            IPEndPoint endPoint,
            IPacketCodec<TId> packetCodec,
            IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Socket = socket;
            socket.ReceiveBufferSize = 8192 * 4;

            _kcp = new PoolSegManager.Kcp(2001, this);
            _kcp.NoDelay(1, 10, 2, 1);//fast
            _kcp.WndSize(128, 128);

            LocalEndPoint = socket.LocalEndPoint as IPEndPoint;
            RemoteEndPoint = endPoint;
        }

        public KcpSession(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            _kcp = new PoolSegManager.Kcp(2001, this);
            _kcp.NoDelay(1, 10, 2, 1);//fast
            _kcp.WndSize(128, 128);

            Connect(endPoint);
        }

        public KcpSession(string addressWithPort, IPacketCodec<TId> packetCodec, IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            _kcp = new PoolSegManager.Kcp(2001, this);
            _kcp.NoDelay(1, 10, 2, 1);//fast
            _kcp.WndSize(128, 128);

            Connect(addressWithPort);
        }

        private bool _closed;
        private bool _isUpdateLoopRunning;
        private readonly PoolSegManager.Kcp _kcp;

        public Channel<ReadOnlyMemory<byte>> DataChannel { get; } = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(1000)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        public Socket? Socket { get; private set; }

        public override bool CanSend => true;
        public override bool CanReceive => true;
        public override bool IsConnected => Socket != null;

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
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket.Connect(RemoteEndPoint!);
        }

        public override ValueTask DoDisconnect()
        {
            if (_closed || Socket == null) return default;

            Socket.Dispose();
            _closed = true;

            return default;
        }

        protected override async Task SendLoop()
        {
            if(!_isUpdateLoopRunning)
                TaskHelper.ManagedRun(UpdateLoop, CancellationTokenSource.Token);

            while (!CancellationTokenSource.IsCancellationRequested)
            {
                if (!IsConnected || !CanSend || !_sendQueue.TryDequeue(out var slice))
                {
                    await Task.Delay(1, CancellationTokenSource.Token);
                    continue;
                }

                await SendOnce(slice);
            }
        }

        private async Task UpdateLoop()
        {
            _isUpdateLoopRunning = true;

            while (!CancellationTokenSource.IsCancellationRequested)
            {
                if (!IsConnected || !CanSend)
                    await SpinWaitAsync.SpinUntil(() => IsConnected && CanReceive);

                _kcp.Update(DateTimeOffset.UtcNow);

                await Task.Delay(10, CancellationTokenSource.Token);
            }

            _isUpdateLoopRunning = false;
        }

        /// <summary>
        /// KCP 发送方法，
        /// 将数据发送至 KCP 库加以处理并排序
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public override ValueTask SendOnce(ReadOnlyMemory<byte> data)
        {
            if (Socket == null)
                throw new InvalidOperationException("Socket Init failed!");

            var result = _kcp.Send(data.Span);
            if(result < 0)
                throw new InvalidOperationException("KCP Send Failed!");

            return default;
        }
        
        public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer)
        {
            if (Socket == null)
                throw new InvalidOperationException("Socket Init failed!");

            var data = ReadOnlyMemory<byte>.Empty;

            while (await DataChannel.Reader.WaitToReadAsync())
            {
                if (DataChannel.Reader.TryRead(out data))
                    break;

                await Task.Delay(10);
            }

            if (data.Length == 0)
                return 0;

            _kcp.Input(data.Span);

            return data.Length;
        }

        /// <summary>
        /// 重写后的接收方法，
        /// 通过持续调用 KCP 库的接收方法来尝试接收数据
        /// </summary>
        /// <returns></returns>
        protected override async Task ReceiveLoop()
        {
            if (!_isUpdateLoopRunning)
                TaskHelper.ManagedRun(UpdateLoop, CancellationTokenSource.Token);

            using var bufferOwner = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
            var buffer = bufferOwner.Memory;

            var receivedLen = 0; //当前共接受了多少数据
            var actualLen = 0;
            var isNewPacket = true;

            var offset = 0; //当前接受到的数据在buffer中的偏移量，buffer中的有效数据：buffer[offset..offset+receivedLen]

            try
            {
                while (!CancellationTokenSource.IsCancellationRequested)
                {
                    if (!IsConnected || !CanReceive) SpinWait.SpinUntil(() => IsConnected && CanReceive);

                    var isInnerReceivedFailed = false;
                    var (received, receivedLength) = _kcp.TryRecv();

                    while (received == null)
                    {
                        var lenThisTime = await ReceiveOnce(Memory<byte>.Empty);

                        if (lenThisTime == 0)
                        {
                            isInnerReceivedFailed = true;
                            break;
                        }
                        
                        (received, receivedLength) = _kcp.TryRecv();

                        await Task.Delay(10);
                    }

                    if (isInnerReceivedFailed) break;
                    if (received == null) break;
                    if (receivedLength <= 0) break;

                    received.Memory[..receivedLength].CopyTo(buffer[(offset + receivedLen)..]);

                    receivedLen += receivedLength;
                    if (isNewPacket && receivedLen >= PacketHeaderLength)
                    {
                        var payloadLen = BitConverter.ToUInt16(buffer.Span.Slice(offset, PacketHeaderLength)); // 获取实际长度(负载长度)
                        actualLen = GetTotalLength(payloadLen);
                        isNewPacket = false;
                    }

                    while (receivedLen >= actualLen) //解决粘包
                    {
                        ProcessPacket(buffer.Slice(offset, actualLen));

                        offset += actualLen;
                        receivedLen -= actualLen;
                        if (receivedLen >= PacketHeaderLength) //还有超过4字节的数据
                        {
                            actualLen = GetTotalLength(BitConverter.ToUInt16(buffer.Span.Slice(offset, PacketHeaderLength)));
                            // 如果receivedLen>=actualLen,那么下一次循环会把这个包处理掉
                            // 如果receivedLen<actualLen,等下一次大循环接收到足够的数据，再处理
                        }
                        else
                        {
                            isNewPacket = true;
                            break;
                        }
                    }

                    if (receivedLen > 0) //没有超过4字节的数据,offset不变，等到下一次Receive的时候继续接收
                        buffer.Slice(offset, receivedLen).CopyTo(buffer);

                    offset = 0;
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
        public async void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            if (Socket == null)
                throw new InvalidOperationException("Socket Init failed!");

            var data = buffer.Memory[..avalidLength];
            var sentLen = 0;

            while (sentLen < avalidLength)
            {
                var sendThisTime = await Socket.SendToAsync(new ArraySegment<byte>(data[sentLen..].ToArray()),
                    SocketFlags.None, RemoteEndPoint);
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