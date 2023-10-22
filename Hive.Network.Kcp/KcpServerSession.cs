using System;
using System.Buffers;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Shared;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Kcp
{
    public class KcpServerSession : KcpSession
    {
        private readonly ArrayBufferWriter<byte> _receiveBuffer = new(NetworkSettings.DefaultBufferSize);
        private readonly ArrayBufferWriter<byte> _sendBuffer = new(NetworkSettings.DefaultBufferSize);

        public KcpServerSession(
            int sessionId,
            IPEndPoint remoteEndPoint,
            IPEndPoint localEndPoint,
            ILogger<KcpSession> logger)
            : base(sessionId, remoteEndPoint, localEndPoint, logger)
        {
            StartKcpLogicAsync(CancellationToken.None);
        }

        public event Func<ArraySegment<byte>, IPEndPoint, CancellationToken, ValueTask<int>>? OnSendAsync;

        public override async ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
        {
            if (!IsConnected)
                return 0;

            _sendBuffer.Clear();

            await Kcp!.OutputAsync(_sendBuffer);

            if (_sendBuffer.WrittenCount == 0) return 0;
            if (OnSendAsync == null) return 0;

            var result = new ArraySegment<byte>(new byte[_sendBuffer.WrittenCount]);
            _sendBuffer.WrittenSpan.CopyTo(result);

            return await OnSendAsync.Invoke(result, RemoteEndPoint, token);
        }

        internal void OnReceived(Memory<byte> memory, CancellationToken token)
        {
            if (!IsConnected) return;
            if (token.IsCancellationRequested) return;

            Logger.LogInformation("UDP raw packet received [length: {len}]", memory.Length);

            Kcp!.Input(memory.Span);
        }

        public override async ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
        {
            if (!IsConnected) return 0;

            _receiveBuffer.Clear();

            await Kcp!.RecvAsync(_receiveBuffer);

            if (_receiveBuffer.WrittenCount > buffer.Count) return 0;

            _receiveBuffer.WrittenSpan.CopyTo(buffer);

            return _receiveBuffer.WrittenCount;
        }
    }
}