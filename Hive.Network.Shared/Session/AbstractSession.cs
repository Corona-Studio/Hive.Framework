using System;
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
            var sendTask = SendLoop(token);
            var fillReceivePipeTask = FillReceivePipeAsync(ReceivePipe!.Writer, token);
            var receiveTask = ReceiveLoop(token);

            return Task.WhenAll(sendTask, fillReceivePipeTask, receiveTask);
        }

        public abstract void Close();

        protected void FireMessageReceived(ReadOnlyMemory<byte> data)
        {
            OnMessageReceived?.Invoke(this, data);
        }

        public abstract ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token);

        public abstract ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token);

        #region Send

        public virtual async ValueTask SendAsync(MemoryStream ms, CancellationToken token = default)
        {
            if (SendPipe == null)
                throw new NullReferenceException(nameof(SendPipe));

            var result = await FillSendPipeAsync(SendPipe.Writer, ms, token);

            if (!result)
                throw new InvalidOperationException($"Failed to fill pipe, data size: {ms.Length}");
        }

        public virtual async ValueTask<bool> TrySendAsync(MemoryStream ms, CancellationToken token = default)
        {
            if (SendPipe == null)
                return false;

            return await FillSendPipeAsync(SendPipe.Writer, ms, token);
        }

        /// <summary>
        ///     将流中的数据复制到 <see cref="SendPipe" />
        ///     <para>Copy and arrange data then send to the <see cref="SendPipe" /></para>
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="stream"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        protected virtual async ValueTask<bool> FillSendPipeAsync(PipeWriter writer, MemoryStream stream,
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
        ///     从 <see cref="SendPipe" /> 读取待发送数据并使用 Socket 发送
        ///     <para>Read from <see cref="SendPipe" /> and send the data using raw socket</para>
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
                    var sequence = result.Buffer;

                    if (!SequenceMarshal.TryGetReadOnlyMemory(sequence, out var buffer))
                        throw new InvalidOperationException(
                            "Failed to create ReadOnlyMemory<byte> from ReadOnlySequence<byte>!");

                    if (!MemoryMarshal.TryGetArray(buffer, out var segment))
                        throw new InvalidOperationException(
                            "Failed to create ArraySegment<byte> from ReadOnlyMemory<byte>!");

                    var totalLen = buffer.Length;
                    var sentLen = 0;

                    while (sentLen < totalLen)
                    {
                        var sendThisTime = await SendOnce(segment[sentLen..], token);

                        sentLen += sendThisTime;
                    }

                    SendPipe.Reader.AdvanceTo(result.Buffer.End);

                    if (result.IsCompleted) return;
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
                        await Task.Delay(10, token);
                        continue;
                    }

                    var result = await ReceivePipe.Reader.ReadAsync(token);
                    var sequence = result.Buffer;

                    if (!SequenceMarshal.TryGetReadOnlyMemory(sequence, out var buffer))
                        throw new InvalidOperationException(
                            "Failed to create ReadOnlyMemory<byte> from ReadOnlySequence<byte>!");

                    // ReSharper disable once RedundantRangeBound
                    var totalLen = BitConverter.ToUInt16(buffer.Span[NetworkSettings.PacketLengthOffset..]);

                    if (totalLen > buffer.Length)
                    {
                        Logger.LogReceiveError(buffer.Length, totalLen);
                        continue;
                    }

                    var bodyLen = totalLen - NetworkSettings.PacketBodyOffset;
                    var data = buffer.Slice(NetworkSettings.PacketBodyOffset, bodyLen);

                    Logger.LogPacketLength(totalLen);
                    Logger.LogBodyLength(bodyLen);

                    FireMessageReceived(data);

                    ReceivePipe.Reader.AdvanceTo(sequence.GetPosition(totalLen));

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
        [LoggerMessage(LogLevel.Error, "Read {ReadLen} bytes from stream, but the stream length is {StreamLength}")]
        public static partial void LogReadBytesFromStreamFailed(this ILogger logger, int readLen, long streamLength);

        [LoggerMessage(LogLevel.Warning, "Send loop canceled, SessionId:{SessionId}")]
        public static partial void LogSendLoopCanceled(this ILogger logger, int sessionId);

        [LoggerMessage(LogLevel.Trace, "Data received from [{endPoint}] with length [{length}]")]
        public static partial void LogDataReceived(this ILogger logger, IPEndPoint endPoint, int length);

        [LoggerMessage(LogLevel.Error, "Received {ReadLen} bytes, but the packet length is {TotalLen}")]
        public static partial void LogReceiveError(this ILogger logger, int readLen, int totalLen);

        [LoggerMessage(LogLevel.Trace, "Packet Length: {length}")]
        public static partial void LogPacketLength(this ILogger logger, int length);

        [LoggerMessage(LogLevel.Trace, "Body Length: {length}")]
        public static partial void LogBodyLength(this ILogger logger, int length);

        [LoggerMessage(LogLevel.Warning, "Receive loop canceled, SessionId:{SessionId}")]
        public static partial void LogReceiveLoopCanceled(this ILogger logger, int sessionId);
    }
}