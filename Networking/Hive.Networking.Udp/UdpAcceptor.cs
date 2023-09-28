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
        
        private readonly IMessageStreamPool _messageStreamPool;
        private readonly Channel<IMessageStream> _messageStreamChannel = Channel.CreateUnbounded<IMessageStream>();

        public UdpAcceptor(IPEndPoint endPoint, IMessageStreamPool messageStreamPool) : base(endPoint)
        {
            _messageStreamPool = messageStreamPool;
            _sessionFactory = ActivatorUtilities.CreateFactory<UdpSession>(new[] {typeof(IPEndPoint), typeof(UdpAcceptor)});
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
            
            _serverSocket.ReceiveBufferSize = UdpSession<int>.DefaultSocketBufferSize;
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
        
        internal async ValueTask<int> SendAsync(IPEndPoint endPoint,IMessageStream messageStream, CancellationToken token)
        {
            var segment = messageStream.GetArraySegment();
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

                if (received == 0) return false;

                
                return true;
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
                _messageStreamPool.Free(stream);
            }
        }
    }
}