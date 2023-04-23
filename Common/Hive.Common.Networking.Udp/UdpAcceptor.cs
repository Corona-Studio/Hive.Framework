﻿using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Helpers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Udp
{
    public sealed class UdpAcceptor<TId, TSessionId> : AbstractAcceptor<UdpClient, UdpSession<TId>, TId, TSessionId> where TId : unmanaged
    {
        public UdpAcceptor(
            IPEndPoint endPoint,
            IPacketCodec<TId> packetCodec,
            IDataDispatcher<UdpSession<TId>> dataDispatcher,
            IClientManager<TSessionId, UdpSession<TId>> clientManager) : base(endPoint, packetCodec, dataDispatcher, clientManager)
        {
        }

        public UdpClient? UdpServer { get; private set; }

        public override void Start()
        {
            UdpServer = new UdpClient(EndPoint.Port);

            TaskHelper.ManagedRun(StartAcceptClient, CancellationTokenSource.Token);
        }

        public override void Stop()
        {
            UdpServer?.Dispose();
        }

        private async Task StartAcceptClient()
        {
            while (!CancellationTokenSource.IsCancellationRequested)
            {
                await DoAcceptClient(UdpServer!, CancellationTokenSource.Token);
            }
        }

        public override async ValueTask DoAcceptClient(UdpClient client, CancellationToken cancellationToken)
        {
            var received = await UdpServer!.ReceiveAsync();
            
            if (ClientManager.TryGetSession(received.RemoteEndPoint, out var session))
            {
                session!.DataQueue.Enqueue(received.Buffer);

                return;
            }

            var clientSession = new UdpSession<TId>(client, received.RemoteEndPoint, PacketCodec, DataDispatcher);

            clientSession.DataQueue.Enqueue(received.Buffer);

            ClientManager.AddSession(clientSession);
        }

        public override void Dispose()
        {
            base.Dispose();
            Stop();
        }
    }
}