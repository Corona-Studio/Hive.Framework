using System;
using System.IO;
using System.Net;
using System.Net.Sockets.Kcp;
using System.Threading.Tasks;
using System.Threading;
using System.Threading.Channels;
using Hive.Network.Shared.Session;
using Hive.Network.Abstractions;
using Microsoft.Extensions.Logging;
using Hive.Network.Shared;
using System.Buffers;

namespace Hive.Network.Kcp
{
    public class KcpSession : AbstractSession
    {
        public KcpSession(
            int sessionId,
            IPEndPoint remoteEndPoint,
            IPEndPoint localEndPoint,
            ILogger<KcpSession> logger,
            IMessageBufferPool messageBufferPool)
            : base(Convert.ToInt32(sessionId), logger, messageBufferPool)
        {
            var conv = (uint)(1u + int.MaxValue + sessionId);

            Conv = conv;

            Kcp = CreateNewKcpManager(conv);

            LocalEndPoint = localEndPoint;
            RemoteEndPoint = remoteEndPoint;
        }

        protected readonly uint Conv;
        
        public UnSafeSegManager.KcpIO? Kcp { get; protected set; }
        public override IPEndPoint LocalEndPoint { get; }
        public override IPEndPoint RemoteEndPoint { get; }
        public override bool CanSend => IsConnected;
        public override bool CanReceive => IsConnected;
        protected override Channel<MemoryStream> SendChannel => throw new NotImplementedException();

        public override Task StartAsync(CancellationToken token)
        {
            var baseTask = base.StartAsync(token);
            var updateTask = Task.Run(() => KcpRawUpdateLoop(token), token);

            return Task.WhenAll(baseTask, updateTask);
        }

        public override async ValueTask<bool> SendAsync(MemoryStream ms, CancellationToken token = default)
        {
            var sendBuffer = ArrayPool<byte>.Shared.Rent(NetworkSettings.DefaultBufferSize);

            ms.Seek(0, SeekOrigin.Begin);

            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            var readLen = await ms.ReadAsync(sendBuffer, NetworkSettings.PacketBodyOffset, (int)ms.Length, token);

            if (readLen != ms.Length)
            {
                Logger.LogError(
                    "Read {0} bytes from stream, but the stream length is {1}",
                    readLen,
                    ms.Length);
                await ms.DisposeAsync();

                return false;
            }

            var totalLen = readLen + NetworkSettings.PacketBodyOffset;

            // 写入头部包体长度字段
            // ReSharper disable once RedundantRangeBound
            BitConverter.TryWriteBytes(
                sendBuffer.AsSpan()[NetworkSettings.PacketLengthOffset..],
                (ushort)totalLen);
            BitConverter.TryWriteBytes(
                sendBuffer.AsSpan()[NetworkSettings.SessionIdOffset..],
                Id);

            var segment = new ArraySegment<byte>(sendBuffer, 0, totalLen);
            var sentLen = 0;

            while (sentLen < segment.Count)
            {
                var sendThisTime = Kcp!.Send(segment[sentLen..]);

                if (sendThisTime < 0)
                    throw new InvalidOperationException("KCP 返回了小于零的发送长度，可能为 KcpCore 的内部错误！");

                sentLen += sendThisTime;
            }

            await ms.DisposeAsync();

            return true;
        }

        protected override async Task SendLoop(CancellationToken token)
        {
            try
            {
                SendingLoopRunning = true;

                while (!token.IsCancellationRequested)
                {
                    await SendOnce(ArraySegment<byte>.Empty, token);
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

                while (!token.IsCancellationRequested)
                {
                    Kcp.Update(DateTimeOffset.UtcNow);

                    await Task.Delay(10, token);
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