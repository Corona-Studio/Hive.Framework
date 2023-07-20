using System;
using System.Net.Sockets;
using System.Net;
using System.Net.Sockets.Kcp;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using System.Buffers;
using System.Diagnostics;
using Hive.Framework.Networking.Shared.Helpers;

namespace Hive.Framework.Networking.Kcp
{
    public sealed class KcpSession<TId> : AbstractSession<TId, KcpSession<TId>>, IKcpCallback, IRentable where TId : unmanaged
    {
        public KcpSession(
            Socket socket,
            IPEndPoint endPoint,
            uint conv,
            IPacketCodec<TId> packetCodec,
            IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Socket = socket;
            socket.ReceiveBufferSize = DefaultSocketBufferSize;

            _conv = conv;
            _connectionReady = true;
            Kcp = CreateNewKcpManager(conv);
            TaskHelper.ManagedRun(UpdateLoop, CancellationTokenSource!.Token);

            LocalEndPoint = socket.LocalEndPoint as IPEndPoint;
            RemoteEndPoint = endPoint;
        }

        public KcpSession(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Connect(endPoint);
        }

        public KcpSession(string addressWithPort, IPacketCodec<TId> packetCodec, IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            Connect(addressWithPort);
        }

        private bool _closed;
        private uint _conv;
        private bool _connectionReady;

        private const int DefaultConnectWaitTime = 100;
        private const int DefaultConnectMaxTrial = 25;

        public const uint UnsetConv = 20010726;
        public UnSafeSegManager.Kcp? Kcp { get; private set; }
        public Socket? Socket { get; private set; }

        public override bool ShouldDestroyAfterDisconnected => false;
        public override bool CanSend => _connectionReady;
        public override bool CanReceive => _connectionReady;
        public override bool IsConnected => Socket != null;

        private UnSafeSegManager.Kcp CreateNewKcpManager(uint conv)
        {
            var kcp = new UnSafeSegManager.Kcp(conv, this, this);
            kcp.NoDelay(1, 10, 2, 1);
            kcp.WndSize(64, 64);
            kcp.SetMtu(512);

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
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            var unsetConvBytes = BitConverter.GetBytes(UnsetConv);
            await Socket.RawSendTo(unsetConvBytes, RemoteEndPoint!);

            var clientRegistered = false;
            var convReceived = false;
            var tryCount = 0;
            var buffer = ArrayPool<byte>.Shared.Rent(1024);

            while (tryCount < DefaultConnectMaxTrial || convReceived)
            {
                tryCount++;

                EndPoint? endPoint = new IPEndPoint(IPAddress.Any, 0);
                var received = Socket.ReceiveFrom(buffer, ref endPoint);

                if (!endPoint.Equals(RemoteEndPoint) || received < sizeof(uint))
                {
                    await Task.Delay(DefaultConnectWaitTime);
                    continue;
                }

                try
                {
                    var assignedConv = BitConverter.ToUInt32(buffer);

                    if (!convReceived)
                    {
                        _conv = assignedConv;
                        convReceived = true;

                        await Socket!.RawSendTo(buffer[..sizeof(uint)], endPoint);

                        await Task.Delay(DefaultConnectWaitTime);
                        continue;
                    }

                    if (_conv != assignedConv || !convReceived) break;

                    clientRegistered = true;
                    break;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    await Task.Delay(DefaultConnectWaitTime);
                }
            }

            ArrayPool<byte>.Shared.Return(buffer);

            if (!clientRegistered)
            {
                await DoDisconnect();
                return;
            }

            Kcp?.Dispose();
            Kcp = CreateNewKcpManager(_conv);
            _connectionReady = true;
            TaskHelper.ManagedRun(UpdateLoop, CancellationTokenSource!.Token);
        }

        public override ValueTask DoDisconnect()
        {
            if (_closed || Socket == null) return default;

            Socket.Dispose();
            _closed = true;
            _connectionReady = false;

            return default;
        }

        private async Task UpdateLoop()
        {
            if (Kcp == null)
                throw new NullReferenceException("Kcp Init Failed!");
            
            while (!(CancellationTokenSource?.IsCancellationRequested ?? true))
            {
                Kcp.Update(DateTimeOffset.UtcNow);

                await Task.Delay(10, CancellationTokenSource.Token);
            }
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

            if (receivedLength > buffer.Length) return 0;

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

        public IMemoryOwner<byte> RentBuffer(int length)
        {
            return MemoryPool<byte>.Shared.Rent(length);
        }
    }
}