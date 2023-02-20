﻿using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Helpers;

namespace Hive.Framework.Networking.Tcp
{
    public sealed class TcpAcceptor<TId, TSessionId> : AbstractAcceptor<Socket, TcpSession<TId>, TId, TSessionId>
    {
        public TcpAcceptor(
            IPEndPoint endPoint,
            IPacketCodec<TId> packetCodec,
            IDataDispatcher<TcpSession<TId>> dataDispatcher,
            IClientManager<TSessionId, TcpSession<TId>> clientManager) : base(endPoint, packetCodec, dataDispatcher, clientManager)
        {
        }

        public Socket? ServerSocket { get; private set; }

        private void InitSocket()
        {
            ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public override void Start()
        {
            if (ServerSocket == null)
                InitSocket();

            ServerSocket!.Bind(EndPoint);
            ServerSocket.Listen(EndPoint.Port);

            TaskHelper.ManagedRun(StartAcceptClient, CancellationTokenSource.Token);
        }

        public override void Stop()
        {
            if (ServerSocket == null) return;

            ServerSocket.Close();
            ServerSocket.Dispose();
        }

        private async Task StartAcceptClient()
        {
            while (!CancellationTokenSource.IsCancellationRequested)
            {
                var clientSocket = await ServerSocket.AcceptAsync();
                await DoAcceptClient(clientSocket, CancellationTokenSource.Token);
            }
        }

        public override ValueTask DoAcceptClient(Socket client, CancellationToken cancellationToken)
        {
            var clientSession = new TcpSession<TId>(client, PacketCodec, DataDispatcher);

            ClientManager.AddSession(clientSession);

            return default;
        }

        public override void Dispose()
        {
            base.Dispose();
            Stop();
        }
    }
}