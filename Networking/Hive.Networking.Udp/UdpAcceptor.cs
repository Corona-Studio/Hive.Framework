using System;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;

namespace Hive.Framework.Networking.Udp
{
    public sealed class UdpAcceptor<TId, TSessionId> : AbstractAcceptor<Socket, UdpSession<TId>, TId, TSessionId>
        where TId : unmanaged
        where TSessionId : unmanaged
    {


        private Socket? _serverSocket;

        public UdpAcceptor(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<UdpSession<TId>> dataDispatcher, IClientManager<TSessionId, UdpSession<TId>> clientManager, ISessionCreator<UdpSession<TId>, Socket> sessionCreator) : base(endPoint, packetCodec, dataDispatcher, clientManager, sessionCreator)
        {
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

                if (ClientManager.TryGetSession((IPEndPoint)endPoint, out var session))
                {
                    await session!.DataChannel.Writer.WriteAsync(buffer.AsMemory()[..received], token);

                    return false;
                }

                var clientSession =
                    new UdpSession<TId>(_serverSocket, (IPEndPoint)endPoint, Codec, DataDispatcher);

                await clientSession.DataChannel.Writer.WriteAsync(buffer.AsMemory()[..received], token);

                ClientManager.AddSession(clientSession);
                return true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}