using System;
using System.Net.Sockets;
using System.Net;
using System.Net.Sockets.Kcp;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using Hive.Framework.Networking.Shared.Helpers;

namespace Hive.Framework.Networking.Kcp
{
    public sealed class KcpSession<TId> : AbstractSession<TId, KcpSession<TId>>, IKcpCallback where TId : unmanaged
    {
        public KcpSession(
            UdpClient socket,
            IPEndPoint endPoint,
            IPacketCodec<TId> packetCodec,
            IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            UdpConnection = socket;

            _kcp = new PoolSegManager.Kcp(2001, this);

            RemoteEndPoint = endPoint;
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
        private bool _isUpdateLoopRunning;
        private readonly PoolSegManager.Kcp _kcp;

        public ConcurrentQueue<byte[]> DataQueue { get; } = new ConcurrentQueue<byte[]>();
        public UdpClient? UdpConnection { get; private set; }

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
                {
                    await Task.Delay(1, CancellationTokenSource.Token);
                    continue;
                }

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
            if (UdpConnection == null)
                throw new InvalidOperationException("Socket Init failed!");

            var result = _kcp.Send(data.Span);
            if(result < 0)
                throw new InvalidOperationException("KCP Send Failed!");

            return default;
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

            _kcp.Input(data);

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
            
            try
            {
                while (!CancellationTokenSource.IsCancellationRequested)
                {
                    if (!IsConnected || !CanReceive) SpinWait.SpinUntil(() => IsConnected && CanReceive);

                    var isInnerReceivedFailed = false;
                    var (received, _) = _kcp.TryRecv();

                    while (received == null)
                    {
                        var lenThisTime = await ReceiveOnce(Memory<byte>.Empty);

                        if (lenThisTime == 0)
                        {
                            isInnerReceivedFailed = true;
                            break;
                        }
                        
                        (received, _) = _kcp.TryRecv();

                        await Task.Delay(10);
                    }

                    if (isInnerReceivedFailed) break;
                    if(received == null) break;

                    ProcessPacket(received.Memory);
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
            if (UdpConnection == null)
                throw new InvalidOperationException("Socket Init failed!");

            var data = buffer.Memory.Span[..avalidLength];
            var sentLen = 0;

            while (sentLen < avalidLength)
            {
                var sendThisTime = UdpConnection.Send(data[sentLen..].ToArray(), data.Length - sentLen, RemoteEndPoint);
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