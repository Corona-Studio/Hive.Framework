using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Abstractions.EventArgs;
using Hive.Framework.Networking.Shared.Helpers;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Shared
{
    /// <summary>
    /// 连接会话抽象
    /// </summary>
    /// <typeparam name="TId">封包 ID 类型（通常为 ushort）</typeparam>
    /// <typeparam name="TSession">连接会话类型 例如在 TCP 实现下，其类型为 TcpSession{TId}</typeparam>
    public abstract class AbstractSession<TId, TSession> : ISession<TSession>, ISender<TId>, ICanRedirectPacket<TId>, IHasCodec<TId>
        where TSession : ISession<TSession>
        where TId : unmanaged
    {
        protected const int DefaultBufferSize = 40960;
        public const int DefaultSocketBufferSize = 8192 * 4; 
        protected const int PacketHeaderLength = sizeof(ushort); // 包头长度2Byte
        
        protected Channel<ReadOnlyMemory<byte>>? SendChannel;
        protected CancellationTokenSource? CancellationTokenSource;
        protected bool ReceivingLoopRunning;
        protected bool SendingLoopRunning;

        public IPacketCodec<TId> PacketCodec { get; }
        public IDataDispatcher<TSession> DataDispatcher { get; }
        public IPEndPoint? LocalEndPoint { get; protected set; }
        public IPEndPoint? RemoteEndPoint { get; protected set; }
        public ISet<TId>? RedirectPacketIds { get; set; }
        public bool RedirectReceivedData { get; set; }

        public abstract bool ShouldDestroyAfterDisconnected { get; }
        public abstract bool CanSend { get; }
        public abstract bool CanReceive { get; }
        public bool Running => !(CancellationTokenSource?.IsCancellationRequested ?? true) && SendingLoopRunning && ReceivingLoopRunning;
        public abstract bool IsConnected { get; }

        public AsyncEventHandler<ReceivedDataEventArgs>? OnDataReceived { get; set; }

        protected AbstractSession(IPacketCodec<TId> packetCodec, IDataDispatcher<TSession> dataDispatcher)
        {
            ResetCancellationToken(new CancellationTokenSource());
            ResetSendChannel(Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(1024)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest
            }));

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

        public virtual async ValueTask SendAsync(ReadOnlyMemory<byte> data)
        {
            if (CancellationTokenSource == null)
                throw new ArgumentNullException(nameof(CancellationTokenSource));
            if (SendChannel == null)
                throw new ArgumentNullException(nameof(SendChannel));

            if (await SendChannel.Writer.WaitToWriteAsync(CancellationTokenSource.Token))
                await SendChannel.Writer.WriteAsync(data);

            if (SendingLoopRunning) return;

            BeginSend();

            SendingLoopRunning = true;
        }

        public async ValueTask SendAsync<T>(T obj, PacketFlags flags)
        {
            if (obj == null) throw new ArgumentNullException($"The data trying to send [{nameof(obj)}] is null!");

            var encodedBytes = PacketCodec.Encode(obj, flags);
            await SendAsync(encodedBytes);
        }

        public void OnReceive<T>(Action<IPacketDecodeResult<T>, TSession> callback)
        {
            DataDispatcher.Register(callback);

            if (ReceivingLoopRunning) return;

            BeginReceive();

            ReceivingLoopRunning = true;
        }

        public void OnReceive<T>(Func<IPacketDecodeResult<T>, TSession, ValueTask> callback)
        {
            DataDispatcher.Register(callback);

            if (ReceivingLoopRunning) return;

            BeginReceive();

            ReceivingLoopRunning = true;
        }

        public void OnReceiveOneTime<T>(Action<IPacketDecodeResult<T>, TSession> callback)
        {
            DataDispatcher.OneTimeRegister(callback);

            if (ReceivingLoopRunning) return;

            BeginReceive();

            ReceivingLoopRunning = true;
        }

        public void OnReceiveOneTime<T>(Func<IPacketDecodeResult<T>, TSession, ValueTask> callback)
        {
            DataDispatcher.OneTimeRegister(callback);

            if (ReceivingLoopRunning) return;

            BeginReceive();

            ReceivingLoopRunning = true;
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

        protected abstract ValueTask DispatchPacket(IPacketDecodeResult<object>? packet, Type? packetType = null);

        protected async ValueTask ProcessPacket(ReadOnlyMemory<byte> payloadBytes)
        {
            var idMemory = PacketCodec.GetPacketIdMemory(payloadBytes);
            var packetFlags = PacketCodec.GetPacketFlags(payloadBytes);
            var id = PacketCodec.GetPacketId(idMemory);

            var isPacketFinalized = packetFlags.HasFlag(PacketFlags.Finalized);
            var shouldRedirect = RedirectReceivedData && (RedirectPacketIds?.Contains(id) ?? false);

            if ((shouldRedirect || packetFlags.HasFlag(PacketFlags.S2CPacket)) && !isPacketFinalized)
            {
                await InvokeDataReceivedEventAsync(idMemory, payloadBytes.ToArray().AsMemory());
                
                return;
            }

            var packet = PacketCodec.Decode(payloadBytes.Span);
            var packetType = PacketCodec.PacketIdMapper.GetPacketType(id);

            await DispatchPacket(packet, packetType);
        }

        protected async Task InvokeDataReceivedEventAsync(ReadOnlyMemory<byte> id, ReadOnlyMemory<byte> data)
        {
            await (OnDataReceived?.InvokeAsync(this, new ReceivedDataEventArgs(id, data)) ?? Task.CompletedTask);
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
                    if (SendChannel == null) throw new InvalidOperationException(nameof(SendChannel));
                    if (!await SendChannel.Reader.WaitToReadAsync(CancellationTokenSource.Token)) break;

                    var slice = await SendChannel.Reader.ReadAsync(CancellationTokenSource.Token);
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
            var bufferOwner = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
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

                    if (actualLen == 0) continue;

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

                        await ProcessPacket(buffer.Slice(offset, actualLen));

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
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
            finally
            {
                ReceivingLoopRunning = false;
                bufferOwner.Dispose();
            }
        }

        public virtual ValueTask DoConnect()
        {
            ResetCancellationToken(new CancellationTokenSource());
            ResetSendChannel(Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(1024)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest
            }));

            return default;
        }

        public virtual ValueTask DoDisconnect()
        {
            ResetCancellationToken();
            ResetSendChannel();

            return default;
        }

        protected void ResetCancellationToken(CancellationTokenSource? cancellationToken = null)
        {
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();
            CancellationTokenSource = cancellationToken;
        }

        protected void ResetSendChannel(Channel<ReadOnlyMemory<byte>>? channel = null)
        {
            SendChannel?.Writer?.TryComplete();
            SendChannel = channel;
        }

        public abstract ValueTask SendOnce(ReadOnlyMemory<byte> data);

        public abstract ValueTask<int> ReceiveOnce(Memory<byte> buffer);

        public virtual void Dispose()
        {
            DoDisconnect();
        }
    }
}