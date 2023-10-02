using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Network.Abstractions;
using Hive.Network.Shared;
using Hive.Network.Shared.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Udp
{
    public sealed class UdpAcceptor : AbstractAcceptor<UdpSession>
    {
        private Socket? _serverSocket;
        private readonly ObjectFactory<UdpServerSession> _sessionFactory;

        private readonly ReaderWriterLockSlim _dictLock = new ReaderWriterLockSlim();
        private readonly Dictionary<int, UdpServerSession> _udpSessions = new Dictionary<int, UdpServerSession>();

        private readonly Channel<IMessageBuffer> _messageStreamChannel = Channel.CreateUnbounded<IMessageBuffer>();

        public UdpAcceptor(IServiceProvider serviceProvider, ILogger<UdpAcceptor> logger) : base(serviceProvider, logger)
        {
            _sessionFactory =
                ActivatorUtilities.CreateFactory<UdpServerSession>(new[]
                    { typeof(int), typeof(IPEndPoint), typeof(IPEndPoint) });
        }

        private void InitSocket(IPEndPoint listenEndPoint)
        {
            _serverSocket = new Socket(listenEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        }

        public override IPEndPoint? EndPoint => _serverSocket?.LocalEndPoint as IPEndPoint;

        public override Task<bool> SetupAsync(IPEndPoint listenEndPoint, CancellationToken token)
        {
            if (_serverSocket == null)
                InitSocket(listenEndPoint);

            if (_serverSocket == null)
            {
                throw new NullReferenceException("ServerSocket is null and InitSocket failed.");
            }

            _serverSocket.ReceiveBufferSize = NetworkSetting.DefaultSocketBufferSize;
            _serverSocket.Bind(listenEndPoint);

            return Task.FromResult(true);
        }

        public override Task<bool> CloseAsync(CancellationToken token)
        {
            if (_serverSocket == null) return Task.FromResult(false);

            _serverSocket.Close();
            _serverSocket.Dispose();
            _serverSocket = null;

            return Task.FromResult(true);
        }

        internal async ValueTask<int> SendAsync(ArraySegment<byte> segment, IPEndPoint endPoint,
            CancellationToken token)
        {
            var len = await _serverSocket.SendToAsync(segment, SocketFlags.None, endPoint);
            return len;
        }

        private readonly byte[] _receiveBuffer = new byte[NetworkSetting.DefaultBufferSize];

        public override async ValueTask<bool> DoOnceAcceptAsync(CancellationToken token)
        {
            if (_serverSocket == null)
                return false;
            
            try
            {
                IPEndPoint? endPoint = new IPEndPoint(IPAddress.Any, 0);
                var arraySegment = new ArraySegment<byte>(_receiveBuffer, 0, _receiveBuffer.Length);
                var receivedArg = await _serverSocket.ReceiveFromAsync(arraySegment, SocketFlags.None, endPoint);

                var received = receivedArg.ReceivedBytes;
                endPoint = (IPEndPoint)receivedArg.RemoteEndPoint;
                
                var headMem = _receiveBuffer.AsMemory(0, NetworkSetting.PacketHeaderLength);
                var length = BitConverter.ToUInt16(headMem.Span[NetworkSetting.PacketLengthOffset..]);
                var sessionId = BitConverter.ToInt32(headMem.Span[NetworkSetting.SessionIdOffset..]);
                if (length != received)
                    return false;

                if (sessionId == NetworkSetting.HandshakeSessionId)
                {
                    var handshake = HandShakePacket.ReadFrom(
                        _receiveBuffer.AsSpan()[NetworkSetting.PacketBodyOffset..]);
                    HandShakePacket next = default;
                    if (handshake.IsServerFinished())
                    {
                        var id = GetNextSessionId();
                        var session = CreateUdpSession(id, endPoint,(IPEndPoint)_serverSocket.LocalEndPoint);
                        next = handshake.CreateFinal(id);
                        if (_dictLock.TryEnterWriteLock(10))
                        {
                            try
                            {
                                _udpSessions.Add(id, session);
                                FireOnSessionCreate(session);
                            }
                            finally
                            {
                                _dictLock.ExitWriteLock();
                            }
                        }
                        else
                        {
                            logger.LogError("Enter write lock failed.");
                            return false;
                        }
                    }
                    else
                    {
                        next = handshake.Next();
                    }
                    next.WriteTo(_receiveBuffer.AsSpan()[NetworkSetting.PacketBodyOffset..]);
                    await SendAsync(
                        new ArraySegment<byte>(_receiveBuffer, 0, NetworkSetting.PacketBodyOffset + HandShakePacket.Size), 
                        (IPEndPoint)endPoint, 
                        token);
                }
                else
                {
                    if (_dictLock.TryEnterReadLock(10))
                    {
                        try
                        {
                            if (_udpSessions.TryGetValue(sessionId, out var session))
                            {
                                // Copy one time
                                session.OnReceived(_receiveBuffer.AsMemory()[..length], token);
                            }
                            else
                            {
                                return false;
                            }
                        }
                        finally
                        {
                            _dictLock.ExitReadLock();
                        }
                    }
                    else
                    {
                        logger.LogError("Enter read lock failed.");
                        return false;
                    }
                }
                return received != 0;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Accept failed.");
                throw;
            }
        }

        private UdpServerSession CreateUdpSession(int id, IPEndPoint remoteEndPoint, IPEndPoint localEndPoint)
        {
            var session = _sessionFactory.Invoke(ServiceProvider, new object[]
            {
                id,
                remoteEndPoint,
                localEndPoint,
            });
            session.OnSendAsync += SendAsync;
            return session;
        }

        public override void Dispose()
        {
            _serverSocket?.Dispose();
            _dictLock.Dispose();

            _messageStreamChannel.Writer.TryComplete();
            while (_messageStreamChannel.Reader.TryRead(out var stream))
            {
                stream.Dispose();
            }
        }
    }
}