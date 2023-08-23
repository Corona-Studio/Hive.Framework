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
using System.Threading;
using Hive.Framework.Networking.Shared.Attributes;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Kcp
{
    public sealed class KcpSession<TId> : AbstractSession<TId, KcpSession<TId>>
        where TId : unmanaged
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
            _passiveMode = true;

            Kcp = CreateNewKcpManager(conv);
            TaskHelper.ManagedRun(UpdateLoop, CancellationTokenSource!.Token);

            BeginSend();
            SendingLoopRunning = true;

            LocalEndPoint = socket.LocalEndPoint as IPEndPoint;
            RemoteEndPoint = endPoint;
        }

        public KcpSession(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            _passiveMode = false;
            Connect(endPoint);
        }

        public KcpSession(string addressWithPort, IPacketCodec<TId> packetCodec, IDataDispatcher<KcpSession<TId>> dataDispatcher) : base(packetCodec, dataDispatcher)
        {
            _passiveMode = false;
            Connect(addressWithPort);
        }

        private bool _closed;
        private uint _conv;
        private bool _connectionReady;

        // 指示是否为被动模式，如果是被动模式，则所有数据将通过 Acceptor 接受后注入 KCP
        // 如果为非被动模式，则使用内部的 Receive 方法从 Socket 接收数据
        private readonly bool _passiveMode;
        
        private readonly ArrayBufferWriter<byte> _sendBuffer = new ArrayBufferWriter<byte>(1024);
        private readonly ArrayBufferWriter<byte> _receiveBuffer = new ArrayBufferWriter<byte>(1024);

        private const int DefaultConnectWaitTime = 100;
        private const int DefaultConnectMaxTrial = 25;

        public const uint UnsetConv = 20010726;
        public UnSafeSegManager.KcpIO? Kcp { get; private set; }
        public Socket? Socket { get; private set; }

        public override bool ShouldDestroyAfterDisconnected => false;
        public override bool CanSend => _connectionReady;
        public override bool CanReceive => _connectionReady;
        public override bool IsConnected => Socket != null;

        private UnSafeSegManager.KcpIO CreateNewKcpManager(uint conv)
        {
            var kcp = new UnSafeSegManager.KcpIO(conv);
            kcp.NoDelay(2, 5, 2, 1);
            kcp.WndSize(1024, 1024);
            //kcp.SetMtu(512);
            kcp.fastlimit = -1;

            return kcp;
        }

        protected override async ValueTask DispatchPacket(PacketDecodeResult<object?> packet, Type? packetType = null)
        {
            await DataDispatcher.DispatchAsync(this, packet, packetType);
        }

        public override async ValueTask DoConnect()
        {
            // 释放先前的连接
            await DoDisconnect();

            ResetCancellationToken(new CancellationTokenSource());

            // 创建新连接
            _closed = false;

            if (RemoteEndPoint == null)
                throw new ArgumentNullException(nameof(RemoteEndPoint));

            Socket = new Socket(RemoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            Socket.ReceiveBufferSize = DefaultSocketBufferSize;

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
            
            TaskHelper.ManagedRun(UpdateLoop, CancellationTokenSource!.Token);
            TaskHelper.ManagedRun(NonPassiveModeRawReceiveLoop, CancellationTokenSource!.Token);

            _connectionReady = true;
        }

        public override ValueTask DoDisconnect()
        {
            if (_closed || Socket == null) return default;

            Socket.Dispose();
            _closed = true;
            _connectionReady = false;

            return default;
        }

        [IgnoreException(typeof(ObjectDisposedException))]
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

        public override async ValueTask SendAsync(ReadOnlyMemory<byte> data)
        {
            if (CancellationTokenSource == null)
                throw new ArgumentNullException(nameof(CancellationTokenSource));

            await SpinWaitAsync.SpinUntil(() => CanSend);

            if(Kcp == null)
                throw new ArgumentNullException(nameof(Kcp));

            var sentLen = 0;
            var sendData = data;

            while (sentLen < sendData.Length)
            {
                var sendThisTime = Kcp.Send(sendData.Span[sentLen..]);

                if (sendThisTime < 0)
                    throw new InvalidOperationException("KCP 返回了小于零的发送长度，可能为 KcpCore 的内部错误！");

                sentLen += sendThisTime;

                await Task.Delay(1);
            }

            if (SendingLoopRunning) return;

            BeginSend();

            SendingLoopRunning = true;
        }

        protected override async Task SendLoop()
        {
            try
            {
                while (!(CancellationTokenSource?.IsCancellationRequested ?? true) || _closed)
                {
                    if (!IsConnected || !CanSend) SpinWait.SpinUntil(() => IsConnected && CanSend);

                    await SendOnce(Memory<byte>.Empty);
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
            finally
            {
                SendingLoopRunning = false;
            }
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
            if (Kcp == null)
                throw new NullReferenceException("Kcp Init Failed!");
            if (Socket == null)
                throw new NullReferenceException(nameof(Socket));
            
            var sentLen = 0;

            await Kcp.OutputAsync(_sendBuffer);

            var sendData = _sendBuffer.WrittenMemory;

            while (sentLen < sendData.Length)
            {
                if (!Socket.Connected)
                    await Socket.ConnectAsync(RemoteEndPoint!);

                var sendThisTime = await Socket.SendAsync(
                    sendData[sentLen..],
                    SocketFlags.None);

                sentLen += sendThisTime;
            }

            _sendBuffer.Clear();
        }

        [IgnoreException(typeof(ObjectDisposedException))]
        private async Task NonPassiveModeRawReceiveLoop()
        {
            if (Kcp == null)
                throw new NullReferenceException("Kcp Init Failed!");
            if (Socket == null)
                throw new NullReferenceException(nameof(Socket));
            if (CancellationTokenSource == null)
                throw new ArgumentNullException(nameof(CancellationTokenSource));
            if (_passiveMode)
                throw new InvalidOperationException("该方法仅支持在非被动模式下启用！");

            while (!(CancellationTokenSource?.IsCancellationRequested ?? true) || _closed)
            {
                if (Socket.Available <= 0)
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
                    
                    Kcp.Input(buffer[..received]);
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
            if (Kcp == null)
                throw new NullReferenceException("Kcp Init Failed!");

            _receiveBuffer.Clear();

            await Kcp.RecvAsync(_receiveBuffer);

            if (_receiveBuffer.WrittenCount > buffer.Length) return 0;

            _receiveBuffer.WrittenMemory.CopyTo(buffer);
            
            return _receiveBuffer.WrittenCount;
        }

        public override void Dispose()
        {
            base.Dispose();
            Kcp?.Dispose();
        }
    }
}