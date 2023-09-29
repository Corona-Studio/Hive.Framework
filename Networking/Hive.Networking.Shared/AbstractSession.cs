using System;
using System.Buffers;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Framework.Networking.Abstractions;
using Microsoft.Extensions.Logging;

namespace Hive.Framework.Networking.Shared
{
    /// <summary>
    /// 连接会话抽象
    /// </summary>
    public abstract class AbstractSession : ISession, IDisposable
    {
        private ILogger<AbstractSession> _logger;
        protected readonly IMessageBufferPool MessageBufferPool;
        
        protected virtual Channel<IMessageBuffer>? SendChannel { get; set; } = Channel.CreateBounded<IMessageBuffer>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        
        protected bool ReceivingLoopRunning;
        protected bool SendingLoopRunning;

        protected AbstractSession(int id, ILogger<AbstractSession> logger, IMessageBufferPool messageBufferPool)
        {
            _logger = logger;
            MessageBufferPool = messageBufferPool;
            Id = id;
        }

        public int Id { get; } // todo: 用于标识Session的唯一ID
        public abstract IPEndPoint? LocalEndPoint { get; }
        public abstract IPEndPoint? RemoteEndPoint { get; }
        public event Action<ISession, ReadOnlyMemory<byte>>? OnMessageReceived;
        
        protected void FireMessageReceived(ReadOnlyMemory<byte> data)
        {
            OnMessageReceived?.Invoke(this, data);
        }

        public async ValueTask<bool> SendAsync(IMessageBuffer buffer, CancellationToken token = default)
        {
            if (SendChannel == null)
                return false;
            
            if (await SendChannel.Writer.WaitToWriteAsync(token))
                return SendChannel.Writer.TryWrite(buffer);
            
            return false;
        }

        public abstract bool CanSend { get; }
        public abstract bool CanReceive { get; }
        public bool Running => SendingLoopRunning && ReceivingLoopRunning;
        
        public abstract bool IsConnected { get; }
        public IMessageBuffer CreateStream()
        {
            var stream = MessageBufferPool.Rent();
            
            // 写入头部
            var span = stream.GetSpan(NetworkSetting.PacketHeaderLength);
            BitConverter.TryWriteBytes(span.Slice(2,sizeof(int)), Id);
            stream.Advance(NetworkSetting.PacketHeaderLength);
            
            return stream;
        }
        
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
                //var headBuffer = new Memory<byte>(new byte[PacketHeaderLength]);
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
                    var data = stream.GetFinalBufferMemory();
                    

                    var totalLen = data.Length;
                    
                    // 写入头部包体长度字段
                    BitConverter.TryWriteBytes(data.Span[..2], (ushort)totalLen);
                    
                    var sentLen = 0;
                    
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

        
        protected virtual async Task ReceiveLoop(CancellationToken token)
        {
            var bufferOwner = MemoryPool<byte>.Shared.Rent(NetworkSetting.DefaultBufferSize);
            var buffer = bufferOwner.Memory;
            
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!IsConnected || !CanReceive)
                    {
                        await Task.Delay(10, token);
                        continue;
                    }

                    var lenThisTime = await ReceiveOnce(buffer, token);

                    if (lenThisTime == 0)
                    {
                        // Logger.LogError("Received 0 bytes, the buffer may be full");
                        break;
                    }

                    var data = buffer.Slice(0, lenThisTime);
                    FireMessageReceived(data);
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