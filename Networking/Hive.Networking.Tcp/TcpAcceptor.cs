using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Attributes;

namespace Hive.Framework.Networking.Tcp
{
    public sealed class TcpAcceptor<TId, TSessionId> : AbstractAcceptor<Socket, TcpSession<TId>, TId, TSessionId>
        where TId : unmanaged
        where TSessionId : unmanaged
    {
        private Socket? _serverSocket;

        public override bool IsValid => _serverSocket != null;

        public TcpAcceptor(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<TcpSession<TId>> dataDispatcher, IClientManager<TSessionId, TcpSession<TId>> clientManager, ISessionCreator<TcpSession<TId>, Socket> sessionCreator) : base(endPoint, packetCodec, dataDispatcher, clientManager, sessionCreator)
        {
        }

        private void InitSocket()
        {
            _serverSocket = new Socket(EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public override Task<bool> SetupAsync(CancellationToken token)
        {
            if (_serverSocket == null)
                InitSocket();

            if (_serverSocket == null)
            {
                throw new NullReferenceException("ServerSocket is null and InitSocket failed.");
            }

            _serverSocket.Bind(EndPoint);
            _serverSocket.Listen(EndPoint.Port);
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

        [IgnoreSocketException(SocketError.OperationAborted)]
        public override Task StartAcceptLoop(CancellationToken token)
        {
            return base.StartAcceptLoop(token);
        }


        public override async ValueTask<bool> DoAcceptAsync(CancellationToken token)
        {
            if (_serverSocket == null)
                return false;

            var acceptSocket = await _serverSocket.AcceptAsync();
            var clientSession = SessionCreator.CreateSession(acceptSocket, (IPEndPoint)acceptSocket.RemoteEndPoint);
            ClientManager.AddSession(clientSession);

            return true;
        }
    }
}