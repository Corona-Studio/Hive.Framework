using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Shared;
using Hive.Network.Shared.HandShake;
using Hive.Network.Shared.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Kcp
{
    public sealed class KcpAcceptor : AbstractAcceptor<KcpSession>
    {
        private readonly ConcurrentDictionary<IPEndPoint, KcpServerSession> _kcpSessions = new();
        private readonly ObjectFactory<KcpServerSession> _sessionFactory;
        private Socket? _serverSocket;

        public KcpAcceptor(
            IServiceProvider serviceProvider,
            ILogger<KcpAcceptor> logger)
            : base(serviceProvider, logger)
        {
            _sessionFactory =
                ActivatorUtilities.CreateFactory<KcpServerSession>(new[]
                    { typeof(int), typeof(IPEndPoint), typeof(IPEndPoint) });
        }

        public override IPEndPoint? EndPoint => _serverSocket?.LocalEndPoint as IPEndPoint;

        private void InitSocket()
        {
            _serverSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
        }

        public override Task SetupAsync(IPEndPoint listenEndPoint, CancellationToken token)
        {
            if (_serverSocket == null)
                InitSocket();

            if (_serverSocket == null) throw new NullReferenceException("ServerSocket is null and InitSocket failed.");

            _serverSocket.ReceiveBufferSize = NetworkSettings.DefaultSocketBufferSize;
            _serverSocket.Bind(listenEndPoint);

            return Task.FromResult(true);
        }

        public override Task<bool> TryCloseAsync(CancellationToken token)
        {
            if (_serverSocket == null) return Task.FromResult(false);

            _serverSocket.Close();
            _serverSocket.Dispose();
            _serverSocket = null;

            return Task.FromResult(true);
        }

        internal async ValueTask<int> SendAsync(
            ArraySegment<byte> segment,
            IPEndPoint endPoint,
            CancellationToken token)
        {
            var sentLen = 0;

            while (sentLen < segment.Count)
            {
                var len = await _serverSocket.SendToAsync(segment[sentLen..], SocketFlags.None, endPoint);
                sentLen += len;
            }

            return sentLen;
        }

        public override async ValueTask<bool> TryDoOnceAcceptAsync(CancellationToken token)
        {
            if (_serverSocket == null)
                return false;

            var buffer = ArrayPool<byte>.Shared.Rent(NetworkSettings.DefaultBufferSize);
            var arraySegment = new ArraySegment<byte>(buffer);

            try
            {
                var endPoint = new IPEndPoint(IPAddress.Any, 0);
                var receivedArg = await _serverSocket.ReceiveFromAsync(arraySegment, SocketFlags.None, endPoint);

                var received = receivedArg.ReceivedBytes;
                endPoint = (IPEndPoint)receivedArg.RemoteEndPoint;

                var headMem = arraySegment.AsMemory(0, NetworkSettings.PacketHeaderLength);
                // ReSharper disable once RedundantRangeBound
                var length = BitConverter.ToUInt16(headMem.Span[NetworkSettings.PacketLengthOffset..]);
                var sessionId = BitConverter.ToInt32(headMem.Span[NetworkSettings.SessionIdOffset..]);
                var isPendingHandShake = sessionId == NetworkSettings.HandshakeSessionId;

                if (isPendingHandShake && length != received)
                {
                    Logger.LogPacketLengthNotEqualToReceivedLength();
                    Logger.LogReceivedLengthNotEqualToActualLength(received, length);
                    return false;
                }

                if (isPendingHandShake)
                {
                    var handshake =
                        HandShakePacket.ReadFrom(arraySegment.AsSpan()[NetworkSettings.PacketBodyOffset..]);
                    HandShakePacket next;
                    if (handshake.IsServerFinished())
                    {
                        var id = GetNextSessionId();
                        var session = CreateKcpSession(id, endPoint, (IPEndPoint)_serverSocket.LocalEndPoint);

                        next = handshake.CreateFinal(id);

                        _kcpSessions.TryAdd(endPoint, session);
                        FireOnSessionCreate(session);
                    }
                    else
                    {
                        next = handshake.Next();
                    }

                    next.WriteTo(arraySegment.AsSpan()[NetworkSettings.PacketBodyOffset..]);

                    await SendAsync(
                        arraySegment[..(NetworkSettings.PacketBodyOffset + HandShakePacket.Size)],
                        endPoint,
                        token);
                }
                else
                {
                    if (_kcpSessions.TryGetValue(endPoint, out var session))
                        // Copy one time
                        session.OnReceived(arraySegment.AsMemory()[..received], token);
                    else
                        return false;
                }

                return received != 0;
            }
            catch (Exception e)
            {
                Logger.LogAcceptFailed(e);
                throw;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private KcpServerSession CreateKcpSession(int id, IPEndPoint remoteEndPoint, IPEndPoint localEndPoint)
        {
            var session = _sessionFactory.Invoke(ServiceProvider, new object[]
            {
                id,
                remoteEndPoint,
                localEndPoint
            });
            session.OnSendAsync += SendAsync;

            return session;
        }

        public override void Dispose()
        {
            _serverSocket?.Dispose();
        }
    }

    internal static partial class KcpAcceptorLoggers
    {
        [LoggerMessage(LogLevel.Warning, "Packet length is not equal to the received length!")]
        public static partial void LogPacketLengthNotEqualToReceivedLength(this ILogger logger);

        [LoggerMessage(LogLevel.Warning, "Received: [{recv}] Actual: [{actual}]")]
        public static partial void LogReceivedLengthNotEqualToActualLength(this ILogger logger, int recv, int actual);

        [LoggerMessage(LogLevel.Error, "Accept failed.")]
        public static partial void LogAcceptFailed(this ILogger logger, Exception ex);
    }
}