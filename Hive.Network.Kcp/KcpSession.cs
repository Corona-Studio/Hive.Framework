using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets.Kcp;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

            Logger.LogInformation("Conv [{conv}]", conv);

            Conv = conv;

            Kcp = CreateNewKcpManager(conv);

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
            var updateTask = Task.Run(() => KcpRawUpdateLoop(token), token);
            var fillBufferTask = Task.Run(() => FillKcpBufferLoop(token), token);

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
                Logger.LogInformation("Send loop canceled, SessionId:{SessionId}", Id);
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Send loop canceled, SessionId:{SessionId}", Id);
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
            kcp.NoDelay(2, 5, 2, 1);
            kcp.WndSize(1024, 1024);
            //kcp.SetMtu(512);
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
                Logger.LogWarning("KCP instance disposed");
            }
        }

        private async Task FillKcpBufferLoop(CancellationToken token)
        {
            try
            {
                if (Kcp == null)
                    throw new NullReferenceException("Kcp Init Failed!");
                if (SendPipe == null)
                    throw new NullReferenceException(nameof(SendPipe));

                while (!token.IsCancellationRequested && IsConnected)
                {
                    var result = await SendPipe.Reader.ReadAsync(token);
                    var sequence = result.Buffer;

                    if (!SequenceMarshal.TryGetReadOnlyMemory(sequence, out var buffer))
                        throw new InvalidOperationException(
                            "Failed to create ReadOnlyMemory<byte> from ReadOnlySequence<byte>!");

                    var sentLen = 0;

                    while (sentLen < buffer.Length)
                    {
                        var sendThisTime = Kcp!.Send(buffer.Span[sentLen..]);

                        if (sendThisTime < 0)
                            throw new InvalidOperationException("KCP 返回了小于零的发送长度，可能为 KcpCore 的内部错误！");

                        sentLen += sendThisTime;
                    }

                    SendPipe.Reader.AdvanceTo(sequence.Start, sequence.GetPosition(sentLen));

                    if (result.IsCompleted) break;
                }
            }
            catch (ObjectDisposedException)
            {
                Logger.LogWarning("KCP instance disposed");
            }
        }

        public override void Close()
        {
            IsConnected = false;
            Kcp?.Dispose();
            Kcp = null;
        }
    }
}