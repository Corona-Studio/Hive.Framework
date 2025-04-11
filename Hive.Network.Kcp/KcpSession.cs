using System;
using System.Net;
using System.Net.Sockets.Kcp;
using System.Threading;
using System.Threading.Tasks;
using Hive.Common.Shared.Helpers;
using Hive.Network.Shared.Session;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Kcp
{
    public class KcpSession : AbstractSession
    {
        protected readonly uint Conv;

        public KcpSession(
            int sessionId,
            IPEndPoint remoteEndPoint,
            IPEndPoint localEndPoint,
            ILogger<KcpSession> logger)
            : base(Convert.ToInt32(sessionId), logger)
        {
            var conv = (uint)(1u + int.MaxValue + sessionId);

            Logger.LogConv(conv);

            Conv = conv;

            Kcp = CreateNewKcpManager(Conv);

            LocalEndPoint = localEndPoint;
            RemoteEndPoint = remoteEndPoint;
        }

        public UnSafeSegManager.KcpIO? Kcp { get; protected set; }
        public override IPEndPoint LocalEndPoint { get; }
        public override IPEndPoint RemoteEndPoint { get; }
        public override bool CanSend => IsConnected;
        public override bool CanReceive => IsConnected;

        public virtual Task StartKcpLogicAsync(CancellationToken token)
        {
            var updateTask = TaskHelper.Fire(() => KcpRawUpdateLoop(token)).Unwrap();
            var fillBufferTask = TaskHelper.Fire(() => FillKcpBufferLoop(token)).Unwrap();

            return Task.WhenAll(updateTask, fillBufferTask);
        }

        protected override async Task SendLoop(CancellationToken token)
        {
            try
            {
                SendingLoopRunning = true;

                while (!token.IsCancellationRequested && IsConnected)
                {
                    await SendOnce(ArraySegment<byte>.Empty, token);
                    await Task.Delay(1, token);
                }
            }
            catch (TaskCanceledException)
            {
                Logger.LogSendLoopCanceled(Id);
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

        public override ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        private static UnSafeSegManager.KcpIO CreateNewKcpManager(uint conv)
        {
            var kcp = new UnSafeSegManager.KcpIO(conv);
            kcp.NoDelay(1, 10, 2, 1);
            kcp.WndSize(128, 128);
            kcp.SetMtu(512);
            kcp.fastlimit = -1;

            return kcp;
        }

        private async Task KcpRawUpdateLoop(CancellationToken token)
        {
            try
            {
                if (Kcp == null)
                    throw new NullReferenceException("Kcp Init Failed!");

                while (!token.IsCancellationRequested && IsConnected)
                {
                    var timeToWait = Kcp.Check(DateTimeOffset.UtcNow);
                    var waitMs = timeToWait.Millisecond < 10 ? 10 : timeToWait.Millisecond;

                    await Task.Delay(waitMs, token);

                    Kcp.Update(DateTimeOffset.UtcNow);
                }
            }
            catch (ObjectDisposedException)
            {
                Logger.LogKcpInstanceDisposed();
            }
        }

        private async Task FillKcpBufferLoop(CancellationToken token)
        {
            if (Kcp == null)
                throw new NullReferenceException("Kcp Init Failed!");
            if (SendPipe == null)
                throw new NullReferenceException(nameof(SendPipe));

            try
            {
                while (!token.IsCancellationRequested && IsConnected)
                {
                    var result = await SendPipe.Reader.ReadAsync(token);
                    var buffer = result.Buffer;

                    var totalLen = buffer.Length;
                    var sentLen = 0;

                    while (sentLen < totalLen && IsConnected)
                    {
                        foreach (var seq in buffer)
                        {
                            var seqSent = 0;

                            while (seqSent < seq.Length)
                            {
                                var sendThisTime = Kcp!.Send(seq.Span[sentLen..]);

                                seqSent += sendThisTime;
                            }

                            sentLen += seqSent;
                        }
                    }

                    SendPipe.Reader.AdvanceTo(buffer.End);

                    if (result.IsCompleted) break;
                }
            }
            catch (ObjectDisposedException)
            {
                Logger.LogKcpInstanceDisposed();
            }
        }

        public override void Close()
        {
            base.Close();

            IsConnected = false;
            Kcp?.Dispose();
            Kcp = null;
        }
    }

    internal static partial class KcpSessionLoggers
    {
        [LoggerMessage(LogLevel.Information, "Conv [{conv}]")]
        public static partial void LogConv(this ILogger logger, uint conv);

        [LoggerMessage(LogLevel.Information, "Send loop canceled, SessionId:{SessionId}")]
        public static partial void LogSendLoopCanceled(this ILogger logger, int sessionId);

        [LoggerMessage(LogLevel.Warning, "KCP instance disposed")]
        public static partial void LogKcpInstanceDisposed(this ILogger logger);
    }
}