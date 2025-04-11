using System;
using System.Buffers;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Shared;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Kcp
{
    public sealed class KcpServerSession : KcpSession
    {
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

            var sendBuffer = new ArrayBufferWriter<byte>(NetworkSettings.DefaultBufferSize);

            await Kcp!.OutputAsync(sendBuffer);

            if (sendBuffer.WrittenCount == 0) return 0;
            if (OnSendAsync == null) return 0;

            var result = new ArraySegment<byte>(new byte[sendBuffer.WrittenCount]);
            sendBuffer.WrittenSpan.CopyTo(result);

            return await OnSendAsync.Invoke(result, RemoteEndPoint, token);
        }

        internal void OnReceived(Memory<byte> memory, CancellationToken token)
        {
            if (!IsConnected) return;
            if (token.IsCancellationRequested) return;

            Logger.LogReceiveClient(memory.Length);

            Kcp!.Input(memory.Span);
        }

        public override async ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
        {
            if (!IsConnected) return 0;

            var receiveLen = await Kcp!.RecvAsync(buffer);

            return receiveLen;
        }
    }

    internal static partial class KcpServerSessionLoggers
    {
        [LoggerMessage(LogLevel.Information, "UDP raw packet received [length: {len}]")]
        public static partial void LogReceiveClient(this ILogger logger, int len);
    }
}