using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hive.Common.Shared.Helpers;
using Hive.Common.Shared.Pooling;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.Logging;
using Microsoft.IO;

namespace Hive.Network.Shared.Session
{
    /// <summary>
    ///     连接会话抽象
    /// </summary>
    public abstract class AbstractSession : ISession, IDisposable
    {
        private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
        private readonly SemaphoreSlim _wireSendSemaphore = new(1, 1);
        private readonly byte[] _borrowedSendHeader = new byte[NetworkSettings.PacketBodyOffset];

        protected readonly ILogger<AbstractSession> Logger;

        protected bool ReceivingLoopRunning;
        protected bool SendingLoopRunning;

        protected CancellationTokenSource? CancellationTokenSource;

        protected AbstractSession(
            int id,
            ILogger<AbstractSession> logger)
        {
            Logger = logger;

            Id = id;
        }

        protected Pipe? SendPipe { get; set; } = new(new PipeOptions(pool: PooledMemoryPool.Shared, minimumSegmentSize: RecyclableMemoryStreamManager.DefaultBlockSize));
        protected Pipe? ReceivePipe { get; set; } = new(new PipeOptions(pool: PooledMemoryPool.Shared, minimumSegmentSize: RecyclableMemoryStreamManager.DefaultBlockSize));

        public abstract bool CanSend { get; }
        public abstract bool CanReceive { get; }
        public virtual bool IsConnected { get; protected set; } = true;
        public bool Running => SendingLoopRunning && ReceivingLoopRunning;

        public virtual void Dispose()
        {
            _sendSemaphore.Dispose();
            _wireSendSemaphore.Dispose();

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

        public event SessionReceivedHandler? OnMessageReceived;

        private SessionReceivedAsyncHandler? _receiveHandler;

        /// <summary>The receive loop awaits this consumer before advancing the pipe.</summary>
        public SessionReceivedAsyncHandler? ReceiveHandler
        {
            get => Volatile.Read(ref _receiveHandler);
            set
            {
                // A multicast async delegate only returns the last ValueTask,
                // which could release the buffer while another consumer uses it.
                if (value != null && value.GetInvocationList().Length != 1)
                    throw new ArgumentException("Only one asynchronous receive consumer is supported.", nameof(value));
                Volatile.Write(ref _receiveHandler, value);
            }
        }

        public virtual Task StartAsync(CancellationToken token)
        {
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

            var linkedToken = CancellationTokenSource.Token;
            var sendTask = TaskHelper.Fire(() => SendLoop(linkedToken)).Unwrap();
            var fillReceivePipeTask = TaskHelper.Fire(() => FillReceivePipeAsync(ReceivePipe!.Writer, linkedToken)).Unwrap();
            var receiveTask = TaskHelper.Fire(() => ReceiveLoop(linkedToken)).Unwrap();

            return Task.WhenAll(sendTask, fillReceivePipeTask, receiveTask);
        }

        public virtual void Close()
        {
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();
            CancellationTokenSource = null;
        }

        protected void FireMessageReceived(ReadOnlySequence<byte> buffer)
        {
            OnMessageReceived?.Invoke(this, buffer);
        }

        public abstract ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token);

        public abstract ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token);

        #region Send

        public virtual async ValueTask SendAsync(Stream ms, CancellationToken token = default)
        {
            if (SendPipe == null)
                throw new NullReferenceException(nameof(SendPipe));

            await TrySendAsync(ms, token);
        }

        public virtual async ValueTask<bool> TrySendAsync(Stream ms, CancellationToken token = default)
        {
            if (SendPipe == null)
                return false;

            var acquired = false;
            try
            {
                await _sendSemaphore.WaitAsync(token);
                acquired = true;
                return await FillSendPipeAsync(SendPipe.Writer, ms, token);
            }
            catch (Exception e)
            {
                Logger.LogSendDataFailed(e);
                return false;
            }
            finally
            {
                if (acquired)
                    _sendSemaphore.Release();
            }
        }

#if NET10_0_OR_GREATER
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
#endif
        protected async ValueTask<bool> SendBorrowedSequenceAsync(
            ReadOnlySequence<byte> payload, CancellationToken token)
        {
            if (payload.Length > ushort.MaxValue - NetworkSettings.PacketBodyOffset)
                throw new ArgumentOutOfRangeException(nameof(payload));
            var acquired = false;
            var started = false;
            try
            {
                await _wireSendSemaphore.WaitAsync(token);
                acquired = true;
                if (!IsConnected || token.IsCancellationRequested) return false;
                BitConverter.TryWriteBytes(_borrowedSendHeader.AsSpan(),
                    (ushort)(payload.Length + NetworkSettings.PacketBodyOffset));
                BitConverter.TryWriteBytes(_borrowedSendHeader.AsSpan(NetworkSettings.SessionIdOffset), Id);
                started = true;
                await SendSegmentAsync(_borrowedSendHeader, token);
                foreach (var memory in payload)
                    await SendSegmentAsync(memory, token);
                return true;
            }
            catch (Exception e)
            {
                // A partial frame cannot be recovered on the same byte stream.
                if (started) Close();
                Logger.LogSendDataFailed(e);
                return false;
            }
            finally
            {
                if (acquired) _wireSendSemaphore.Release();
            }
        }

#if NET10_0_OR_GREATER
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
#endif
        protected async ValueTask<bool> SendWritableFrameAsync(Memory<byte> frame, CancellationToken token)
        {
            if (frame.Length < NetworkSettings.PacketBodyOffset || frame.Length > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(frame));
            var acquired = false;
            var started = false;
            try
            {
                await _wireSendSemaphore.WaitAsync(token).ConfigureAwait(false);
                acquired = true;
                if (!IsConnected || token.IsCancellationRequested) return false;
                BitConverter.TryWriteBytes(frame.Span, (ushort)frame.Length);
                BitConverter.TryWriteBytes(frame.Span.Slice(NetworkSettings.SessionIdOffset), Id);
                started = true;
                await SendSegmentAsync(frame, token).ConfigureAwait(false);
                return true;
            }
            catch (Exception e)
            {
                if (started) Close();
                Logger.LogSendDataFailed(e);
                return false;
            }
            finally
            {
                if (acquired) _wireSendSemaphore.Release();
            }
        }

#if NET10_0_OR_GREATER
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
#endif
        private async ValueTask SendSegmentAsync(ReadOnlyMemory<byte> memory, CancellationToken token)
        {
            if (!MemoryMarshal.TryGetArray(memory, out var segment))
                throw new InvalidOperationException("The transport requires array-backed memory.");
            var sent = 0;
            while (sent < segment.Count)
            {
                token.ThrowIfCancellationRequested();
                var count = await SendOnce(segment[sent..], token);
                if (count <= 0 || count > segment.Count - sent)
                    throw new IOException("The transport stopped before the frame was sent.");
                sent += count;
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
            Stream stream,
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

                while (!token.IsCancellationRequested && IsConnected)
                {
                    var result = await SendPipe.Reader.ReadAsync(token);
                    var buffer = result.Buffer;

                    var acquired = false;
                    try
                    {
                        await _wireSendSemaphore.WaitAsync(token);
                        acquired = true;
                        foreach (var memory in buffer)
                            await SendSegmentAsync(memory, token);
                        Logger.LogDataSent(RemoteEndPoint!, (int)buffer.Length);
                    }
                    catch
                    {
                        Close();
                        throw;
                    }
                    finally
                    {
                        if (acquired) _wireSendSemaphore.Release();
                        SendPipe.Reader.AdvanceTo(buffer.End);
                    }

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
            try
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

                    if (flushResult.IsCompleted || flushResult.IsCanceled) break;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Normal shutdown; completion wakes the receive reader.
            }
            finally
            {
                await writer.CompleteAsync();
            }
        }

        private static ushort ReadPacketLength(ReadOnlySequence<byte> buffer)
        {
            var reader = new SequenceReader<byte>(buffer);
            if (!reader.TryReadLittleEndian(out short length))
                throw new InvalidDataException("Missing packet length.");
            return unchecked((ushort)length);
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
                        ReceivePipe.Reader.AdvanceTo(buffer.End);
                        break;
                    }

                    var consumed = buffer.Start;
                    var examined = buffer.Start;

                    try
                    {
                        while (buffer.Length > 0)
                        {
                            if (buffer.Length < NetworkSettings.PacketBodyOffset)
                            {
                                // Not enough data to read the packet header
                                examined = buffer.End;
                                break;
                            }

                            // ReSharper disable once RedundantRangeBound
                            var totalLen = ReadPacketLength(buffer);
                            if (totalLen < NetworkSettings.PacketBodyOffset)
                                throw new InvalidDataException("Packet length is smaller than its header.");

                            if (totalLen > buffer.Length)
                            {
                                // Not enough data to read the whole packet
                                Logger.LogPacketIsNotLongEnough(buffer.Length, totalLen);
                                examined = buffer.End;
                                break;
                            }

                            var bodyLen = totalLen - NetworkSettings.PacketBodyOffset;
                            var data = buffer.Slice(NetworkSettings.PacketBodyOffset, bodyLen);

                            Logger.LogPacketLength(totalLen);
                            Logger.LogBodyLength(bodyLen);

                            FireMessageReceived(data);
                            var handler = ReceiveHandler;
                            if (handler != null)
                                await handler(this, data, token);

                            consumed = buffer.GetPosition(totalLen);
                            examined = consumed;
                            buffer = buffer.Slice(totalLen);
                        }
                    }
                    finally
                    {
                        ReceivePipe.Reader.AdvanceTo(consumed, examined);
                    }

                    if (result.IsCompleted)
                    {
                        if (buffer.Length != 0)
                            throw new InvalidDataException("Connection ended with an incomplete frame.");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogReceiveLoopCanceled(Id);
            }
            finally
            {
                ReceivingLoopRunning = false;
                await ReceivePipe.Reader.CompleteAsync();
                Close();
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

        [LoggerMessage(LogLevel.Warning, "Send loop canceled, SessionId: {SessionId}")]
        public static partial void LogSendLoopCanceled(this ILogger logger, int sessionId);

        [LoggerMessage(LogLevel.Trace, "Data received from [{endPoint}] with length [{length}]")]
        public static partial void LogDataReceived(this ILogger logger, IPEndPoint endPoint, int length);

        [LoggerMessage(LogLevel.Trace, "Data sent to [{endPoint}] with length [{length}]")]
        public static partial void LogDataSent(this ILogger logger, IPEndPoint endPoint, int length);

        [LoggerMessage(LogLevel.Debug, "Received {ReadLen} bytes, but the packet length is {TotalLen}, wait for the next iteration...")]
        public static partial void LogPacketIsNotLongEnough(this ILogger logger, long readLen, int totalLen);

        [LoggerMessage(LogLevel.Trace, "Packet Length: {length}")]
        public static partial void LogPacketLength(this ILogger logger, int length);

        [LoggerMessage(LogLevel.Trace, "Body Length: {length}")]
        public static partial void LogBodyLength(this ILogger logger, int length);

        [LoggerMessage(LogLevel.Warning, "Receive loop canceled, SessionId: {SessionId}")]
        public static partial void LogReceiveLoopCanceled(this ILogger logger, int sessionId);
    }
}