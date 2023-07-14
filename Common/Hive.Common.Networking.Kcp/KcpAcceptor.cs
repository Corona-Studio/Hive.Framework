using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Helpers;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Kcp
{
    public sealed class KcpAcceptor<TId, TSessionId> : AbstractAcceptor<Socket, KcpSession<TId>, TId, TSessionId> where TId : unmanaged
    {
        public KcpAcceptor(
            IPEndPoint endPoint,
            IPacketCodec<TId> packetCodec,
            IDataDispatcher<KcpSession<TId>> dataDispatcher,
            IClientManager<TSessionId, KcpSession<TId>> clientManager) : base(endPoint, packetCodec, dataDispatcher, clientManager)
        {
        }

        public Socket? Socket { get; private set; }

        public override void Start()
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket.Bind(EndPoint);

            TaskHelper.ManagedRun(StartAcceptClient, CancellationTokenSource.Token);
        }

        public override void Stop()
        {
            Socket?.Dispose();
        }

        private async Task StartAcceptClient()
        {
            while (!CancellationTokenSource.IsCancellationRequested)
            {
                await DoAcceptClient(Socket!, CancellationTokenSource.Token);
            }
        }

        public override async ValueTask DoAcceptClient(Socket client, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];
            EndPoint? endPoint = new IPEndPoint(IPAddress.Any, 0);
            var received = client.ReceiveFrom(buffer, ref endPoint);

            if (received == 0) return;

            if (ClientManager.TryGetSession((IPEndPoint)endPoint, out var session))
            {
                await session!.DataChannel.Writer.WriteAsync(buffer.AsMemory()[..received], cancellationToken);

                return;
            }

            var clientSession = new KcpSession<TId>(client, (IPEndPoint)endPoint, PacketCodec, DataDispatcher);

            await clientSession.DataChannel.Writer.WriteAsync(buffer.AsMemory()[..received], cancellationToken);

            ClientManager.AddSession(clientSession);
        }

        public override void Dispose()
        {
            base.Dispose();
            Stop();
        }
    }
}