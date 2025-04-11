using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Shared;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Udp
{
    /// <summary>
    ///     基于 Socket 的 UDP 传输层实现
    /// </summary>
    public sealed class UdpServerSession : UdpSession
    {
        public UdpServerSession(
            int sessionId,
            IPEndPoint remoteEndPoint,
            IPEndPoint localEndPoint,
            ILogger<UdpSession> logger)
            : base(sessionId, remoteEndPoint, localEndPoint, logger)
        {
        }

        public event Func<ArraySegment<byte>, IPEndPoint, CancellationToken, ValueTask<int>>? OnSendAsync;

        public override ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
        {
            if (!IsConnected)
                return new ValueTask<int>(0);

            return OnSendAsync?.Invoke(data, RemoteEndPoint, token) ?? new ValueTask<int>(0);
        }

        internal async ValueTask OnReceivedAsync(Memory<byte> memory, CancellationToken token)
        {
            if (!IsConnected) return;
            if (ReceivePipe == null)
                throw new NullReferenceException(nameof(ReceivePipe));

            var buffer = ReceivePipe.Writer.GetMemory(NetworkSettings.DefaultBufferSize);

            memory.Span.CopyTo(buffer.Span);
            ReceivePipe.Writer.Advance(memory.Length);

            var flushResult = await ReceivePipe.Writer.FlushAsync(token);

            if (flushResult.IsCompleted)
                IsConnected = false;
        }

        public override ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
        {
            return new ValueTask<int>(0);
        }
    }
}