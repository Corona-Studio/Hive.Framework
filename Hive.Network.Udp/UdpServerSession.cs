using System;
using System.Buffers;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Udp
{
    /// <summary>
    ///     基于 Socket 的 UDP 传输层实现
    /// </summary>
    public class UdpServerSession : UdpSession
    {
        private readonly Channel<ArraySegment<byte>> _messageStreamChannel =
            Channel.CreateUnbounded<ArraySegment<byte>>();

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

        internal void OnReceived(Memory<byte> memory, CancellationToken token)
        {
            if (!IsConnected) return;

            var bytes = ArrayPool<byte>.Shared.Rent(memory.Length);
            memory.CopyTo(bytes);
            var segment = new ArraySegment<byte>(bytes, 0, memory.Length);
            if (!_messageStreamChannel.Writer.TryWrite(segment))
            {
                ArrayPool<byte>.Shared.Return(bytes);
                IsConnected = false;
            }
        }

        public override async ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
        {
            if (!IsConnected) return 0;

            await _messageStreamChannel.Reader.WaitToReadAsync(token);

            if (!_messageStreamChannel.Reader.TryRead(out var stream)) return 0;

            stream.CopyTo(buffer);
            var len = stream.Count;
            ArrayPool<byte>.Shared.Return(stream.Array);
            return len;
        }
    }
}