using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Shared.Session
{
    /// <summary>
    ///     连接会话抽象
    /// </summary>
    public abstract class AbstractSession : ISession, IDisposable
    {
        private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

        protected readonly ILogger<AbstractSession> Logger;

        protected bool ReceivingLoopRunning;
        protected bool SendingLoopRunning;

        protected AbstractSession(
            int id,
            ILogger<AbstractSession> logger)
        {
            Logger = logger;

            Id = id;
            LastHeartBeatTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        protected Pipe? SendPipe { get; set; } = new();
        protected Pipe? ReceivePipe { get; set; } = new();

        public abstract bool CanSend { get; }
        public abstract bool CanReceive { get; }
        public virtual bool IsConnected { get; protected set; } = true;
        public bool Running => SendingLoopRunning && ReceivingLoopRunning;

        public virtual void Dispose()
        {
            _sendSemaphore.Dispose();

            if (SendPipe != null)
            {
                SendPipe.Reader.Complete();
                SendPipe.Writer.Complete();
                SendPipe = null;
            }

            if (ReceivePipe != null)
            {
                ReceivePipe.Reader.Complete();
                ReceivePipe.Writer.Complete();
                ReceivePipe = null;
            }
        }

        public SessionId Id { get; }
        public abstract IPEndPoint? LocalEndPoint { get; }
        public abstract IPEndPoint? RemoteEndPoint { get; }
        public long LastHeartBeatTime { get; }

        public event SessionReceivedHandler? OnMessageReceived;

        public virtual Task StartAsync(CancellationToken token)
        {
            var sendTask = Task.Run(async () => await SendLoop(token), token);
            var fillReceivePipeTask = Task.Run(async () => await FillReceivePipeAsync(ReceivePipe!.Writer, token), token);
            var receiveTask = Task.Run(async () => await ReceiveLoop(token), token);

            return Task.WhenAll(sendTask, fillReceivePipeTask, receiveTask);
        }

        public abstract void Close();

        protected void FireMessageReceived(ReadOnlySequence<byte> buffer)
        {
            OnMessageReceived?.Invoke(this, buffer);
        }

        public abstract ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token);

        public abstract ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token);

        #region Send

        public virtual async ValueTask SendAsync(MemoryStream ms, CancellationToken token = default)
        {
            if (SendPipe == null)
                throw new NullReferenceException(nameof(SendPipe));

            await TrySendAsync(ms, token);
        }

        public virtual async ValueTask<bool> TrySendAsync(MemoryStream ms, CancellationToken token = default)
        {
            if (SendPipe == null)
                return false;

            try
            {
                await _sendSemaphore.WaitAsync(token);
                return await FillSendPipeAsync(SendPipe.Writer, ms, token);
            }
            catch (Exception e)
            {
                Logger.LogSendDataFailed(e);
                return false;
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        /// <summary>
        /// 将流中的数据复制到 <see cref="SendPipe" />
        /// <para>Copy and arrange data then send to the <see cref="SendPipe" /></para>
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="stream"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        protected virtual async ValueTask<bool> FillSendPipeAsync(
            PipeWriter writer,
            MemoryStream stream,
            CancellationToken token = default)
        {
            stream.Seek(0, SeekOrigin.Begin);

            var memory = writer.GetMemory(NetworkSettings.DefaultBufferSize);
            var readLen = await stream.ReadAsync(memory[NetworkSettings.PacketBodyOffset..], token);

            if (readLen != stream.Length)
            {
                Logger.LogReadBytesFromStreamFailed(readLen, stream.Length);
                await stream.DisposeAsync();

                return false;
            }

            var totalLen = readLen + NetworkSettings.PacketBodyOffset;

            // 写入头部包体长度字段
            // ReSharper disable once RedundantRangeBound
            BitConverter.TryWriteBytes(
                memory.Span[NetworkSettings.PacketLengthOffset..],
                (ushort)totalLen);
            BitConverter.TryWriteBytes(
                memory.Span[NetworkSettings.SessionIdOffset..],
                Id);

            writer.Advance(totalLen);
            await stream.DisposeAsync();

            var flushResult = await writer.FlushAsync(token);

            return !flushResult.IsCompleted;
        }

        /// <summary>
        /// 从 <see cref="SendPipe" /> 读取待发送数据并使用 Socket 发送
        /// <para>Read from <see cref="SendPipe" /> and send the data using raw socket</para>
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        protected virtual async Task SendLoop(CancellationToken token)
        {
            if (SendPipe == null)
                throw new NullReferenceException(nameof(SendPipe));

            try
            {
                SendingLoopRunning = true;

                while (!token.IsCancellationRequested)
                {
                    var result = await SendPipe.Reader.ReadAsync(token);
                    var buffer = result.Buffer;

                    var totalLen = buffer.Length;
                    var sentLen = 0;

                    while (sentLen < totalLen && IsConnected)
                    {
                        foreach (var seq in buffer)
                        {
                            if (!MemoryMarshal.TryGetArray(seq, out var segment))
                                throw new InvalidOperationException(
                                    "Failed to create ArraySegment<byte> from ReadOnlyMemory<byte>!");

                            var sendThisTime = await SendOnce(segment[sentLen..], token);

                            sentLen += sendThisTime;
                        }
                    }

                    Logger.LogDataSent(RemoteEndPoint!, sentLen);

                    SendPipe.Reader.AdvanceTo(result.Buffer.End);

                    if (result.IsCompleted) break;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogSendLoopCanceled(Id);
            }
            finally
            {
                SendingLoopRunning = false;
            }
        }

        #endregion

        #region Receive

        protected virtual async Task FillReceivePipeAsync(PipeWriter writer, CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                var memory = writer.GetMemory(NetworkSettings.DefaultBufferSize);

                if (!MemoryMarshal.TryGetArray<byte>(memory, out var segment))
                    throw new InvalidOperationException(
                        "Failed to create ArraySegment<byte> from ReadOnlyMemory<byte>!");

                var receiveLen = await ReceiveOnce(segment, token);

                if (receiveLen == 0) break;
                if (receiveLen == -1)
                {
                    // Data is not ready yet, wait for a while and try again
                    await Task.Delay(10, token);
                    continue;
                }

                Logger.LogDataReceived(RemoteEndPoint!, receiveLen);

                writer.Advance(receiveLen);

                var flushResult = await writer.FlushAsync(token);

                if (flushResult.IsCompleted) break;
            }
        }

        protected virtual async Task ReceiveLoop(CancellationToken token)
        {
            if (ReceivePipe == null)
                throw new NullReferenceException(nameof(ReceivePipe));

            try
            {
                ReceivingLoopRunning = true;

                while (!token.IsCancellationRequested)
                {
                    if (!IsConnected || !CanReceive)
                    {
                        Logger.LogSocketNotReady(IsConnected, CanReceive);
                        await Task.Delay(10, token);
                        continue;
                    }

                    var result = await ReceivePipe.Reader.ReadAsync(token);
                    var buffer = result.Buffer;

                    if (buffer.Length == 0)
                    {
                        // No more data coming, break the loop
                        break;
                    }

                    var consumed = buffer.Start;
                    var examined = buffer.Start;

                    while (buffer.Length > 0)
                    {
                        if (buffer.Length < NetworkSettings.PacketBodyOffset)
                        {
                            // Not enough data to read the packet header
                            examined = buffer.End;
                            break;
                        }

                        // ReSharper disable once RedundantRangeBound
                        var headerSlice = buffer.Slice(NetworkSettings.PacketLengthOffset, NetworkSettings.PacketBodyOffset);
                        var totalLen = BitConverter.ToUInt16(headerSlice.ToArray().AsSpan());

                        if (totalLen > buffer.Length)
                        {
                            // Not enough data to read the whole packet
                            Logger.LogReceiveError(buffer.Length, totalLen);
                            examined = buffer.End;
                            break;
                        }

                        var bodyLen = totalLen - NetworkSettings.PacketBodyOffset;
                        var data = buffer.Slice(NetworkSettings.PacketBodyOffset, bodyLen);

                        Logger.LogPacketLength(totalLen);
                        Logger.LogBodyLength(bodyLen);

                        FireMessageReceived(data);

                        consumed = buffer.GetPosition(totalLen);
                        examined = consumed;
                        buffer = buffer.Slice(totalLen);
                    }

                    ReceivePipe.Reader.AdvanceTo(consumed, examined);

                    if (result.IsCompleted) break;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogReceiveLoopCanceled(Id);
            }
            finally
            {
                ReceivingLoopRunning = false;
            }
        }

        #endregion
    }

    internal static partial class AbstractSessionLoggers
    {
        [LoggerMessage(LogLevel.Critical, "[Recv] Failed to send data!")]
        public static partial void LogSendDataFailed(this ILogger logger, Exception ex);

        [LoggerMessage(LogLevel.Trace, "[Recv] Socket is not ready yet! [Connected: {isConnected}] [CanReceive {canReceive}]")]
        public static partial void LogSocketNotReady(this ILogger logger, bool isConnected, bool canReceive);

        [LoggerMessage(LogLevel.Error, "Read {ReadLen} bytes from stream, but the stream length is {StreamLength}")]
        public static partial void LogReadBytesFromStreamFailed(this ILogger logger, int readLen, long streamLength);

        [LoggerMessage(LogLevel.Warning, "Send loop canceled, SessionId:{SessionId}")]
        public static partial void LogSendLoopCanceled(this ILogger logger, int sessionId);

        [LoggerMessage(LogLevel.Trace, "Data received from [{endPoint}] with length [{length}]")]
        public static partial void LogDataReceived(this ILogger logger, IPEndPoint endPoint, int length);

        [LoggerMessage(LogLevel.Trace, "Data sent to [{endPoint}] with length [{length}]")]
        public static partial void LogDataSent(this ILogger logger, IPEndPoint endPoint, int length);

        [LoggerMessage(LogLevel.Error, "Received {ReadLen} bytes, but the packet length is {TotalLen}")]
        public static partial void LogReceiveError(this ILogger logger, long readLen, int totalLen);

        [LoggerMessage(LogLevel.Trace, "Packet Length: {length}")]
        public static partial void LogPacketLength(this ILogger logger, int length);

        [LoggerMessage(LogLevel.Trace, "Body Length: {length}")]
        public static partial void LogBodyLength(this ILogger logger, int length);

        [LoggerMessage(LogLevel.Warning, "Receive loop canceled, SessionId:{SessionId}")]
        public static partial void LogReceiveLoopCanceled(this ILogger logger, int sessionId);
    }
}