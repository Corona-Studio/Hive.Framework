using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared.Attributes;
using Microsoft.Extensions.Logging;

namespace Hive.Framework.Networking.Shared
{
    /// <summary>
    /// 连接会话抽象
    /// </summary>
    public abstract class AbstractSession : ISession, IDisposable
    {
        private ILogger<AbstractSession> _logger;
        public const int DefaultBufferSize = 40960;
        public const int DefaultSocketBufferSize = 8192 * 4; 
        protected const int PacketHeaderLength = sizeof(ushort); // 包头长度2Byte
        
        protected virtual Channel<IMessageStream>? SendChannel { get; set; } = Channel.CreateBounded<IMessageStream>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        
        protected bool ReceivingLoopRunning;
        protected bool SendingLoopRunning;

        protected AbstractSession(ILogger<AbstractSession> logger)
        {
            _logger = logger;
        }

        public int Id { get; } // todo: 用于标识Session的唯一ID
        public abstract IPEndPoint? LocalEndPoint { get; }
        public abstract IPEndPoint? RemoteEndPoint { get; }
        public event Action<ISession, ReadOnlyMemory<byte>>? OnMessageReceived;
        
        protected void FireMessageReceived(ReadOnlyMemory<byte> data)
        {
            OnMessageReceived?.Invoke(this, data);
        }

        public async ValueTask<bool> SendAsync(IMessageStream stream, CancellationToken token = default)
        {
            if (SendChannel == null)
                return false;
            
            if (await SendChannel.Writer.WaitToWriteAsync(token))
                return SendChannel.Writer.TryWrite(stream);
            
            return false;
        }

        public abstract bool CanSend { get; }
        public abstract bool CanReceive { get; }
        public bool Running => SendingLoopRunning && ReceivingLoopRunning;
        
        public abstract bool IsConnected { get; }

        public Task StartAsync(CancellationToken token)
        {
            var sendTask = Task.Run(()=>SendLoop(token), token);
            var receiveTask = Task.Run(()=>ReceiveLoop(token), token);

            return Task.WhenAll(sendTask, receiveTask);
        }
        
        protected virtual async Task SendLoop(CancellationToken token)
        {
            try
            {
                var headBuffer = new Memory<byte>(new byte[PacketHeaderLength]);
                SendingLoopRunning = true;
                while (!token.IsCancellationRequested)
                {
                    if (SendChannel == null) throw new InvalidOperationException(nameof(SendChannel));
                    if (!IsConnected || !CanSend || !await SendChannel.Reader.WaitToReadAsync(token))
                    {
                        await Task.Delay(10, token);
                        continue;
                    }

                    var stream = await SendChannel.Reader.ReadAsync(token);
                    var data = stream.GetBufferMemory();
                    

                    var totalLen = data.Length;
                    
                    BitConverter.TryWriteBytes(headBuffer.Span, (ushort)totalLen);
                    
                    var sentLen = 0;
                    
                    await SendOnce(headBuffer, token);
                    while (sentLen < totalLen)
                    {
                        var sendThisTime = await SendOnce(data[sentLen..], token);

                        sentLen += sendThisTime;
                    }
                    
                    stream.Dispose();
                }
            }
            finally
            {
                SendingLoopRunning = false;
            }
        }

        [IgnoreException(typeof(ObjectDisposedException))]
        [IgnoreSocketException(SocketError.OperationAborted)]
        protected virtual async Task ReceiveLoop(CancellationToken token)
        {
            var bufferOwner = MemoryPool<byte>.Shared.Rent(DefaultBufferSize);
            var buffer = bufferOwner.Memory;

            var receivedLen = 0; //当前共接受了多少数据
            var actualLen = 0;
            var isNewPacket = true;

            var offset = 0; //当前接受到的数据在buffer中的偏移量，buffer中的有效数据：buffer[offset..offset+receivedLen]

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!IsConnected || !CanReceive)
                    {
                        await Task.Delay(10, token);
                        continue;
                    }

                    var lenThisTime = await ReceiveOnce(buffer[(offset + receivedLen)..], token);

                    if (lenThisTime == 0)
                    {
                        // Logger.LogError("Received 0 bytes, the buffer may be full");
                        break;
                    }

                    receivedLen += lenThisTime;
                    if (isNewPacket && receivedLen >= PacketHeaderLength)
                    {
                        actualLen = BitConverter.ToUInt16(buffer.Span.Slice(offset, PacketHeaderLength)); // 获取实际长度(负载长度)
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
                        
                        OnMessageReceived?.Invoke(this, buffer.Slice(offset+PacketHeaderLength, actualLen));

                        offset += actualLen;
                        receivedLen -= actualLen;
                        if (receivedLen >= PacketHeaderLength) //还有超过4字节的数据
                        {
                            var headMem = buffer.Slice(offset, PacketHeaderLength);
                            actualLen = BitConverter.ToUInt16(headMem.Span);
                            // 如果 receivedLen >= actualLen,那么下一次循环会把这个包处理掉
                            // 如果 receivedLen < actualLen,等下一次大循环接收到足够的数据，再处理
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
                bufferOwner.Dispose();
            }
        }

        public abstract ValueTask<int> SendOnce(ReadOnlyMemory<byte> data, CancellationToken token);

        public abstract ValueTask<int> ReceiveOnce(Memory<byte> buffer, CancellationToken token);

        public virtual void Dispose()
        {
            if (SendChannel == null) return;
            SendChannel.Writer.Complete();
            while (SendChannel.Reader.TryRead(out var stream))
            {
                stream.Dispose();
            }
        }
    }
}