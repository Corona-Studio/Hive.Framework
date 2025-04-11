using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hive.Common.Shared.Helpers;
using Hive.Network.Shared;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Kcp
{
    public sealed class KcpClientSession : KcpSession
    {
        private Socket? _socket;

        public KcpClientSession(
            int sessionId,
            Socket socket,
            IPEndPoint remoteEndPoint,
            ILogger<KcpClientSession> logger)
            : base(sessionId, remoteEndPoint, (IPEndPoint)socket.LocalEndPoint, logger)
        {
            _socket = socket;
            StartKcpLogicAsync(CancellationToken.None);
        }

        public override Task StartKcpLogicAsync(CancellationToken token)
        {
            var baseTask = base.StartKcpLogicAsync(token);
            var receiveTask = TaskHelper.Fire(() => KcpRawReceiveLoop(token)).Unwrap();

            return Task.WhenAll(baseTask, receiveTask);
        }

        public override async ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
        {
            var sentLen = 0;
            var sendBuffer = new ArrayBufferWriter<byte>(NetworkSettings.DefaultBufferSize);

            await Kcp!.OutputAsync(sendBuffer);

            if (!MemoryMarshal.TryGetArray(sendBuffer.WrittenMemory, out var sendData))
                throw new ArgumentException("SendBuffer is not a valid array segment.");

            while (sentLen < sendData.Count)
            {
                var sendThisTime = await _socket.SendToAsync(
                    sendData[sentLen..],
                    SocketFlags.None,
                    RemoteEndPoint);

                sentLen += sendThisTime;
            }

            return sentLen;
        }

        public override async ValueTask<int> ReceiveOnce(ArraySegment<byte> buffer, CancellationToken token)
        {
            if (!IsConnected) return 0;

            var receiveLen = await Kcp!.RecvAsync(buffer);

            Logger.LogReceiveClient(receiveLen);

            return receiveLen;
        }

        private async Task KcpRawReceiveLoop(CancellationToken token)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(NetworkSettings.DefaultBufferSize);
            var segment = new ArraySegment<byte>(buffer);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var receivedResult = await _socket!.ReceiveFromAsync(segment, SocketFlags.None, RemoteEndPoint);
                    var received = receivedResult.ReceivedBytes;

                    if (received == 0)
                    {
                        await Task.Delay(1, token);
                        continue;
                    }

                    Kcp!.Input(segment[..received]);
                }
            }
            catch (Exception ex)
            {
                Logger.LogKcpRawReceiveLoopFailed(ex);
                throw;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public override void Close()
        {
            base.Close();
            _socket?.Close();
            _socket?.Dispose();
            _socket = null;
        }
    }

    internal static partial class KcpClientSessionLoggers
    {
        [LoggerMessage(LogLevel.Error, "KCP raw receive loop failed!")]
        public static partial void LogKcpRawReceiveLoopFailed(this ILogger logger, Exception ex);
    }
}