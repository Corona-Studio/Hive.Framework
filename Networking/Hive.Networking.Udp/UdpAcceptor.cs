using System;
using Hive.Framework.Networking.Shared;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Channels;
using Hive.Framework.Networking.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Hive.Framework.Networking.Udp
{
    public sealed class UdpAcceptor : AbstractAcceptor<UdpSession>
    {
        private Socket? _serverSocket;
        private readonly ObjectFactory<UdpSession> _sessionFactory;
        
        private readonly ReaderWriterLockSlim _dictLock = new ReaderWriterLockSlim();
        private readonly Dictionary<int, UdpSession> _udpSessions = new Dictionary<int, UdpSession>();
        
        private readonly IMessageBufferPool _messageBufferPool;
        private readonly Channel<IMessageBuffer> _messageStreamChannel = Channel.CreateUnbounded<IMessageBuffer>();
        
        public UdpAcceptor(IPEndPoint endPoint, IMessageBufferPool messageBufferPool, IServiceProvider serviceProvider) : base(endPoint, serviceProvider)
        {
            _messageBufferPool = messageBufferPool;
            _sessionFactory = ActivatorUtilities.CreateFactory<UdpSession>(new[] {typeof(int), typeof(IPEndPoint), typeof(UdpAcceptor)});
        }
        
        private void InitSocket()
        {
            _serverSocket = new Socket(EndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        }

        public override Task<bool> SetupAsync(CancellationToken token)
        {
            if (_serverSocket == null)
                InitSocket();

            if (_serverSocket == null)
            {
                throw new NullReferenceException("ServerSocket is null and InitSocket failed.");
            }
            
            _serverSocket.ReceiveBufferSize = NetworkSetting.DefaultSocketBufferSize;
            _serverSocket.Bind(EndPoint);
            
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
        
        internal async ValueTask<int> SendAsync(IPEndPoint endPoint,ArraySegment<byte> segment, CancellationToken token)
        {
            var len = await _serverSocket.SendToAsync(segment, SocketFlags.None, endPoint);
            return len;
        }

        public override async ValueTask<bool> DoAcceptAsync(CancellationToken token)
        {
            if (_serverSocket == null)
                return false;
            
            if (_serverSocket.Available <= 0) return false;

            var buffer = ArrayPool<byte>.Shared.Rent(1024);
            try
            {
                EndPoint? endPoint = new IPEndPoint(IPAddress.Any, 0);
                var received = _serverSocket.ReceiveFrom(buffer, ref endPoint);
                
                var headMem = buffer.AsMemory(0, NetworkSetting.PacketHeaderLength);
                var length = BitConverter.ToUInt16(headMem.Span[NetworkSetting.PacketLengthOffset..]);
                var sessionId = BitConverter.ToInt32(headMem.Span[NetworkSetting.PacketIdOffset..]);
                if (length != received)
                    return false;

                if (sessionId == NetworkSetting.HandshakeSessionId)
                {
                    var id = GetNextSessionId();
                    var session = _sessionFactory.Invoke(ServiceProvider,new object[]{id, (IPEndPoint) endPoint, this});
                    session.OnSend += SendAsync;
                    
                    _dictLock.EnterWriteLock();
                    try
                    {
                        _udpSessions.Add(id, session);
                    }
                    finally
                    {
                        _dictLock.ExitWriteLock();
                    }
                }
                else
                {
                    if (!_dictLock.TryEnterReadLock(10))
                    {
                        return false;
                    }

                    try
                    {
                        if (_udpSessions.TryGetValue(sessionId, out var session))
                        {
                            session.OnReceivedAsync(buffer.AsMemory().Slice(0,length), token);
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

                return received != 0;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public override void Dispose()
        {
            _serverSocket?.Dispose();
            _dictLock.Dispose();
            
            _messageStreamChannel.Writer.TryComplete();
            while (_messageStreamChannel.Reader.TryRead(out var stream))
            {
                _messageBufferPool.Free(stream);
            }
        }
    }
}