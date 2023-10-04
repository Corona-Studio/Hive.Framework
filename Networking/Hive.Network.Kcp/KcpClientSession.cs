using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Buffers;
using Hive.Network.Shared;

namespace Hive.Network.Kcp
{
    public class KcpClientSession : KcpSession
    {
        private Socket? _socket;

        private readonly OpenArrayBufferWriter<byte> _sendBuffer = new();
        private readonly ArrayBufferWriter<byte> _receiveBuffer = new();

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
            var receiveTask = Task.Run(() => KcpRawReceiveLoop(token), token);

            return Task.WhenAll(baseTask, receiveTask);
        }

        public override async ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
        {
            var sentLen = 0;

            await Kcp!.OutputAsync(_sendBuffer);

            var sendData = _sendBuffer.Buffer;
            var segment = new ArraySegment<byte>(sendData, 0, _sendBuffer.WrittenCount);

            while (sentLen < segment.Count)
            {
                var sendThisTime = await _socket.SendToAsync(
                    segment[sentLen..],
                    SocketFlags.None,
                    RemoteEndPoint);

                sentLen += sendThisTime;
            }

            _sendBuffer.Clear();

            return sentLen;
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

        private async Task KcpRawReceiveLoop(CancellationToken token)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(1024);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var segment = new ArraySegment<byte>(buffer);
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
                Logger.LogError(ex, "KCP raw receive loop failed!");
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
}