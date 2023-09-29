using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Hive.Framework.Networking.Tcp
{
    public sealed class TcpAcceptor : AbstractAcceptor<TcpSession>
    {
        private Socket? _serverSocket;

        public override bool IsValid => _serverSocket != null;

        private readonly ObjectFactory<TcpSession> _sessionFactory;

        public TcpAcceptor(IPEndPoint endPoint, IServiceProvider serviceProvider) : base(endPoint,serviceProvider)
        {
            _sessionFactory = ActivatorUtilities.CreateFactory<TcpSession>(new[] {typeof(int), typeof(Socket)});
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
            var sessionId = GetNextSessionId();
            var clientSession = _sessionFactory.Invoke(ServiceProvider, new object[] {sessionId, acceptSocket});
            
            await FireOnSessionAccepted(clientSession);

            return true;
        }

        public override void Dispose()
        {
            _serverSocket?.Dispose();
        }
    }
}