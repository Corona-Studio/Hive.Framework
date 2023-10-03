using System;
using System.Buffers;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Kcp
{
    public class KcpServerSession : KcpSession
    {
        private readonly ArrayBufferWriter<byte> _receiveBuffer = new();

        public KcpServerSession(
            int sessionId,
            IPEndPoint remoteEndPoint,
            IPEndPoint localEndPoint,
            ILogger<KcpSession> logger,
            IMessageBufferPool messageBufferPool)
            : base(sessionId, remoteEndPoint, localEndPoint, logger, messageBufferPool)
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

            Kcp!.Input(memory.Span);
        }

        public override async ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
        {
            if (!IsConnected) return 0;

            _receiveBuffer.Clear();

            await Kcp!.RecvAsync(_receiveBuffer);

            if (_receiveBuffer.WrittenCount > buffer.Count) return 0;

            _receiveBuffer.WrittenMemory.CopyTo(buffer);

            return _receiveBuffer.WrittenCount;
        }
    }
}