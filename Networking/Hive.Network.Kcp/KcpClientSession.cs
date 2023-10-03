using Hive.Network.Abstractions;
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
            ILogger<KcpClientSession> logger,
            IMessageBufferPool messageBufferPool)
            : base(sessionId, remoteEndPoint, (IPEndPoint)socket.LocalEndPoint, logger, messageBufferPool)
        {
            _socket = socket;
        }

        public override Task StartAsync(CancellationToken token)
        {
            var baseTask = base.StartAsync(token);
            var updateTask = Task.Run(() => KcpRawReceiveLoop(token), token);

            return Task.WhenAll(baseTask, updateTask);
        }

        public override async ValueTask<int> SendOnce(ArraySegment<byte> data, CancellationToken token)
        {
            var sentLen = 0;

            await Kcp!.OutputAsync(_sendBuffer);

            var sendData = _sendBuffer.Buffer;
            var segment = new ArraySegment<byte>(sendData, 0, _sendBuffer.WrittenCount);

            while (sentLen < sendData.Length)
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
            var buffer = ArrayPool<byte>.Shared.Rent(NetworkSettings.DefaultBufferSize);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (_socket!.Available <= 0)
                    {
                        await Task.Delay(1, token);
                        continue;
                    }

                    EndPoint? endPoint = new IPEndPoint(IPAddress.Any, 0);
                    var received = _socket.ReceiveFrom(buffer, ref endPoint);

                    if (received == 0 || !endPoint.Equals(RemoteEndPoint))
                    {
                        await Task.Delay(1, token);
                        continue;
                    }

                    Kcp!.Input(buffer[..received]);
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