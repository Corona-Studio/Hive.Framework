﻿using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Shared.Session
{
    /// <summary>
    /// 连接会话抽象
    /// </summary>
    public abstract class AbstractSession : ISession, IDisposable
    {
        protected readonly ILogger<AbstractSession> Logger;
        protected readonly IMessageBufferPool MessageBufferPool;
        
        protected virtual Channel<MemoryStream>? SendChannel { get; set; } = Channel.CreateBounded<MemoryStream>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        
        protected bool ReceivingLoopRunning;
        protected bool SendingLoopRunning;

        protected AbstractSession(int id, ILogger<AbstractSession> logger, IMessageBufferPool messageBufferPool)
        {
            Logger = logger;
            MessageBufferPool = messageBufferPool;
            Id = id;
            LastHeartBeatTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public SessionId Id { get; }
        public abstract IPEndPoint? LocalEndPoint { get; }
        public abstract IPEndPoint? RemoteEndPoint { get; }
        public long LastHeartBeatTime { get; }
        public event EventHandler<ReadOnlyMemory<byte>>? OnMessageReceived;
        
        protected void FireMessageReceived(ReadOnlyMemory<byte> data)
        {
            OnMessageReceived?.Invoke(this, data);
        }

        public async ValueTask<bool> SendAsync(MemoryStream ms, CancellationToken token = default)
        {
            if (SendChannel == null)
                return false;
            
            if (await SendChannel.Writer.WaitToWriteAsync(token))
                return SendChannel.Writer.TryWrite(ms);
            
            return false;
        }

        public abstract void Close();

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
            var sendBuffer = ArrayPool<byte>.Shared.Rent(NetworkSetting.DefaultBufferSize);
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
                    stream.Seek(0, SeekOrigin.Begin);
                    // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                    var readLen = stream.Read(sendBuffer, NetworkSetting.PacketBodyOffset, (int)stream.Length);
                    if (readLen != stream.Length)
                    {
                        Logger.LogError("Read {0} bytes from stream, but the stream length is {1}", readLen,
                            stream.Length);
                        await stream.DisposeAsync();
                        continue;
                    }

                    var totalLen = readLen + NetworkSetting.PacketBodyOffset;

                    // 写入头部包体长度字段
                    BitConverter.TryWriteBytes(sendBuffer.AsSpan()[NetworkSetting.PacketLengthOffset..],
                        (ushort)totalLen);
                    BitConverter.TryWriteBytes(sendBuffer.AsSpan()[NetworkSetting.SessionIdOffset..], Id);

                    var segment = new ArraySegment<byte>(sendBuffer, 0, totalLen);
                    var sentLen = 0;

                    while (sentLen < totalLen)
                    {
                        var sendThisTime = await SendOnce(segment[sentLen..], token);

                        sentLen += sendThisTime;
                    }

                    await stream.DisposeAsync();
                }
            }
            catch (TaskCanceledException e)
            {
                Logger.LogInformation("Send loop canceled, SessionId:{SessionId}", Id);
            }
            catch (OperationCanceledException e)
            {
                Logger.LogInformation("Send loop canceled, SessionId:{SessionId}", Id);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sendBuffer);
                SendingLoopRunning = false;
            }
        }

        
        protected virtual async Task ReceiveLoop(CancellationToken stoppingToken)
        {
            var receiveBuffer = ArrayPool<byte>.Shared.Rent(NetworkSetting.DefaultBufferSize);
            var segment = new ArraySegment<byte>(receiveBuffer);
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (!IsConnected || !CanReceive)
                    {
                        await Task.Delay(10, stoppingToken);
                        continue;
                    }

                    var lenThisTime = await ReceiveOnce(segment, stoppingToken);

                    if (lenThisTime == 0)
                    {
                        // Logger.LogError("Received 0 bytes, the buffer may be full");
                        break;
                    }
                    
                    var totalLen = BitConverter.ToUInt16(segment[NetworkSetting.PacketLengthOffset..]);
                    if (totalLen > lenThisTime)
                    {
                        Logger.LogError("Received {0} bytes, but the packet length is {1}", lenThisTime, totalLen);
                        continue;
                    }
                    var bodyLen = totalLen - NetworkSetting.PacketBodyOffset;
                    var data = segment.Slice(NetworkSetting.PacketBodyOffset, bodyLen);
                    FireMessageReceived(data);
                }
            }catch (TaskCanceledException e)
            {
                Logger.LogInformation("Receive loop canceled, SessionId:{SessionId}", Id);
            }catch (OperationCanceledException e)
            {
                Logger.LogInformation("Receive loop canceled, SessionId:{SessionId}", Id);
            }
            finally
            {
                ReceivingLoopRunning = false;
                ArrayPool<byte>.Shared.Return(receiveBuffer);
            }
        }

        public abstract ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token);

        public abstract ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token);

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