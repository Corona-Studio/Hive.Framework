using System;
using System.Net.Sockets;
using System.Net;
using System.Net.Sockets.Kcp;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using System.Buffers;
using Hive.Framework.Networking.Shared.Helpers;

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

            Kcp = CreateNewKcpManager();

            LocalEndPoint = socket.LocalEndPoint as IPEndPoint;
            RemoteEndPoint = endPoint;
        }

        public KcpSession(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Kcp = CreateNewKcpManager();

            Connect(endPoint);
        }

        public KcpSession(string addressWithPort, IPacketCodec<TId> packetCodec, IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Kcp = CreateNewKcpManager();

            Connect(addressWithPort);
        }

        private bool _closed;
        private bool _isUpdateLoopRunning;

        public UnSafeSegManager.Kcp? Kcp { get; private set; }
        public Socket? Socket { get; private set; }

        public override bool CanSend => true;
        public override bool CanReceive => true;
        public override bool IsConnected => Socket != null;

        private UnSafeSegManager.Kcp CreateNewKcpManager()
        {
            var kcp = new UnSafeSegManager.Kcp(2001, this);
            //kcp.NoDelay(1, 1, 2, 1);//fast
            //kcp.Interval(1);
            //kcp.WndSize(1024, 1024);

            return kcp;
        }

        protected override void DispatchPacket(object? packet, Type? packetType = null)
        {
            if (packet == null) return;

            DataDispatcher.Dispatch(this, packet, packetType);
        }

        public override async ValueTask DoConnect()
        {
            // 释放先前的连接
            await DoDisconnect();
            await base.DoConnect();

            // 创建新连接
            _closed = false;
            Kcp?.Dispose();
            Kcp = CreateNewKcpManager();

            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
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
            if (CancellationTokenSource == null) return;
            if (!_isUpdateLoopRunning)
                TaskHelper.ManagedRun(UpdateLoop, CancellationTokenSource!.Token);

            while (!(CancellationTokenSource?.IsCancellationRequested ?? true))
            {
                if (!IsConnected || !CanSend || !SendQueue.TryDequeue(out var slice))
                {
                    await Task.Delay(1, CancellationTokenSource.Token);
                    continue;
                }

                await SendOnce(slice);
            }

            SendingLoopRunning = false;
        }

        protected override Task ReceiveLoop()
        {
            var task = base.ReceiveLoop();

            if (!_isUpdateLoopRunning)
                TaskHelper.ManagedRun(UpdateLoop, CancellationTokenSource!.Token);

            return task;
        }

        private async Task UpdateLoop()
        {
            if (Kcp == null)
                throw new NullReferenceException("Kcp Init Failed!");

            _isUpdateLoopRunning = true;

            while (!(CancellationTokenSource?.IsCancellationRequested ?? true))
            {
                Kcp.Update(DateTimeOffset.UtcNow);

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
            if (Kcp == null)
                throw new NullReferenceException("Kcp Init Failed!");

            var sentLen = 0;

            while (sentLen < data.Length)
            {
                var result = Kcp.Send(data.Span[sentLen..]);

                if (result < 0)
                    throw new InvalidOperationException("KCP Send Failed!");

                sentLen += result;
            }

            return default;
        }
        
        public override async ValueTask<int> ReceiveOnce(Memory<byte> buffer)
        {
            if (Kcp == null)
                throw new NullReferenceException("Kcp Init Failed!");

            var (received, receivedLength) = Kcp.TryRecv();

            while (received == null)
            {
                await Task.Delay(10);

                (received, receivedLength) = Kcp.TryRecv();
            }

            received.Memory[..receivedLength].CopyTo(buffer);

            return receivedLength;
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
            if (CancellationTokenSource?.IsCancellationRequested ?? true) return;

            var data = buffer.Memory[..avalidLength];
            var sentLen = 0;

            while (sentLen < avalidLength)
            {
                var sendThisTime = Socket.SendTo(data[sentLen..].ToArray(), RemoteEndPoint!);
                sentLen += sendThisTime;
            }

            buffer.Dispose();
        }

        public override void Dispose()
        {
            base.Dispose();
            Kcp?.Dispose();
        }
    }
}