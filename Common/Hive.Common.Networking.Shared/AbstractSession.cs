using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Abstractions.EventArgs;
using Hive.Framework.Networking.Shared.Helpers;

namespace Hive.Framework.Networking.Shared
{
    /// <summary>
    /// 连接会话抽象
    /// </summary>
    /// <typeparam name="TId">封包 ID 类型（通常为 ushort）</typeparam>
    /// <typeparam name="TSession">连接会话类型 例如在 TCP 实现下，其类型为 TcpSession{TId}</typeparam>
    public abstract class AbstractSession<TId, TSession> : ISession<TSession>, ISender<TId>, ICanRedirectPacket<TId>, IHasCodec<TId> where TSession : ISession<TSession> where TId : unmanaged
    {
        protected const int DefaultBufferSize = 40960;
        protected const int DefaultSocketBufferSize = 8192 * 4; 
        protected const int PacketHeaderLength = sizeof(ushort); // 包头长度2Byte

        protected readonly ConcurrentQueue<ReadOnlyMemory<byte>> SendQueue = new ();
        protected CancellationTokenSource? CancellationTokenSource;
        protected bool ReceivingLoopRunning;
        protected bool SendingLoopRunning;

        public IPacketCodec<TId> PacketCodec { get; }
        public IDataDispatcher<TSession> DataDispatcher { get; }
        public IPEndPoint? LocalEndPoint { get; protected set; }
        public IPEndPoint? RemoteEndPoint { get; protected set; }
        public TId[]? ExcludeRedirectPacketIds { get; set; }
        public bool RedirectReceivedData { get; set; }

        public abstract bool CanSend { get; }
        public abstract bool CanReceive { get; }
        public bool Running => !(CancellationTokenSource?.IsCancellationRequested ?? true) && SendingLoopRunning && ReceivingLoopRunning;
        public abstract bool IsConnected { get; }

        public event EventHandler<ReceivedDataEventArgs>? OnDataReceived;

        protected AbstractSession(IPacketCodec<TId> packetCodec, IDataDispatcher<TSession> dataDispatcher)
        {
            ResetCancellationToken(new CancellationTokenSource());

            PacketCodec = packetCodec;
            DataDispatcher = dataDispatcher;
        }

        protected void Connect(string addressWithPort)
        {
            Connect(NetworkAddressHelper.ToIpEndPoint(addressWithPort));
        }

        protected void Connect(IPEndPoint remoteEndPoint)
        {
            RemoteEndPoint = remoteEndPoint;
            DoConnect();
        }

        public virtual void Send(ReadOnlyMemory<byte> data)
        {
            SendQueue.Enqueue(data);

            if (SendingLoopRunning) return;

            BeginSend();

            SendingLoopRunning = true;
        }

        public void Send<T>(T obj)
        {
            if (obj == null) throw new ArgumentNullException($"The data trying to send [{nameof(obj)}] is null!");

            var encodedBytes = PacketCodec.Encode(obj);
            Send(encodedBytes);
        }

        public void OnReceive<T>(Action<T, TSession> callback) // 用于兼容旧的基于Action的回调
        {
            DataDispatcher.Register(callback);

            if (ReceivingLoopRunning) return;

            BeginReceive();

            ReceivingLoopRunning = true;
        }

        public void RemoveOnReceive<T>(Action<T, TSession> callback)
        {
            DataDispatcher.Unregister(callback);
        }

        public void BeginSend()
        {
            if (CancellationTokenSource == null)
                throw new ArgumentNullException(nameof(CancellationTokenSource));

            TaskHelper.ManagedRun(SendLoop, CancellationTokenSource.Token);
        }
        
        public void BeginReceive()
        {
            if (CancellationTokenSource == null)
                throw new ArgumentNullException(nameof(CancellationTokenSource));

            TaskHelper.ManagedRun(ReceiveLoop, CancellationTokenSource.Token);
        }

        protected abstract void DispatchPacket(object? packet, Type? packetType = null);

        protected void ProcessPacket(ReadOnlyMemory<byte> payloadBytes)
        {
            var idMemory = PacketCodec.GetPacketIdMemory(payloadBytes);
            var id = PacketCodec.GetPacketId(idMemory);

            if (RedirectReceivedData && !(ExcludeRedirectPacketIds?.Contains(id) ?? false))
            {
                InvokeDataReceivedEvent(idMemory, payloadBytes);

                return;
            }

            var packet = PacketCodec.Decode(payloadBytes.Span);
            var packetType = PacketCodec.PacketIdMapper.GetPacketType(id);

            DispatchPacket(packet.Payload, packetType);
        }

        protected void InvokeDataReceivedEvent(ReadOnlyMemory<byte> id, ReadOnlyMemory<byte> data)
        {
            OnDataReceived?.Invoke(this, new ReceivedDataEventArgs(id, data));
        }

        /// <summary>
        ///     根据负载长度获取包的总长度，即包体长度+包头长度
        /// </summary>
        protected static int GetTotalLength(int payloadLength)
        {
            return payloadLength + PacketHeaderLength;
        }

        protected virtual async Task SendLoop()
        {
            try
            {
                while (!(CancellationTokenSource?.IsCancellationRequested ?? true))
                {
                    if (!IsConnected || !CanSend) SpinWait.SpinUntil(() => IsConnected && CanSend);
                    if (!SendQueue.TryDequeue(out var slice))
                    {
                        await Task.Delay(1, CancellationTokenSource.Token);
                        continue;
                    }

                    await SendOnce(slice);
                }
            }
            catch (Exception)
            {
                SendingLoopRunning = false;
            }
        }

        protected virtual async Task ReceiveLoop()
        {
            using var bufferOwner = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
            var buffer = bufferOwner.Memory;

            var receivedLen = 0; //当前共接受了多少数据
            var actualLen = 0;
            var isNewPacket = true;

            var offset = 0; //当前接受到的数据在buffer中的偏移量，buffer中的有效数据：buffer[offset..offset+receivedLen]

            try
            {
                while (!(CancellationTokenSource?.IsCancellationRequested ?? true))
                {
                    if (!IsConnected || !CanReceive) SpinWait.SpinUntil(() => IsConnected && CanReceive);

                    var lenThisTime = await ReceiveOnce(buffer[(offset + receivedLen)..]);

                    if (lenThisTime == 0)
                    {
                        // Logger.LogError("Received 0 bytes, the buffer may be full");
                        break;
                    }

                    receivedLen += lenThisTime;
                    if (isNewPacket && receivedLen >= PacketHeaderLength)
                    {
                        var payloadLen = BitConverter.ToUInt16(buffer.Span.Slice(offset, PacketHeaderLength)); // 获取实际长度(负载长度)
                        actualLen = GetTotalLength(payloadLen);
                        isNewPacket = false;
                    }

                    /*
#if TRACE
                    if (RemoteEndPoint is IPEndPoint remoteEndPoint)
                        Logger.LogTrace("接收 {RemoteIP}:{RemotePort} 发来的 [{LenThisTime}/{ActualLen}] 字节",
                            remoteEndPoint.Address, remoteEndPoint.Port, lenThisTime, actualLen);
#endif
                    */

                    while (receivedLen >= actualLen) //解决粘包
                    {
                        /*
#if TRACE
                        Logger.LogTrace("集齐 {ActualLen} 字节 开始处理处理数据包", actualLen);
#endif
                        */

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

        public virtual ValueTask DoConnect()
        {
            ResetCancellationToken(new CancellationTokenSource());

            return default;
        }

        public virtual ValueTask DoDisconnect()
        {
            ResetCancellationToken();

            return default;
        }

        protected void ResetCancellationToken(CancellationTokenSource? cancellationToken = null)
        {
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();
            CancellationTokenSource = cancellationToken;
        }

        public abstract ValueTask SendOnce(ReadOnlyMemory<byte> data);

        public abstract ValueTask<int> ReceiveOnce(Memory<byte> buffer);

        public virtual void Dispose()
        {
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();
            DoDisconnect();
        }
    }
}