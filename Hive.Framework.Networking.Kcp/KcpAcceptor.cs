using System.Net;
using Hive.Framework.Networking.Shared;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared.Helpers;

namespace Hive.Framework.Networking.Kcp
{
    public sealed class KcpAcceptor<TId, TSessionId> : AbstractAcceptor<Socket, KcpSession<TId>, TId, TSessionId>
    {
        public KcpAcceptor(
            IPEndPoint endPoint,
            IPacketCodec<TId> packetCodec,
            IDataDispatcher<KcpSession<TId>> dataDispatcher,
            IClientManager<TSessionId, KcpSession<TId>> clientManager) : base(endPoint, packetCodec, dataDispatcher, clientManager)
        {
        }

        public Socket? ServerSocket { get; private set; }

        private void InitSocket()
        {
            ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Udp);
        }

        public override void Start()
        {
            if (ServerSocket == null)
                InitSocket();

            ServerSocket!.Bind(EndPoint);
            ServerSocket.Listen(EndPoint.Port);

            TaskHelper.ManagedRun(StartAcceptClient, _cancellationTokenSource.Token);
        }

        public override void Stop()
        {
            if (ServerSocket == null) return;

            ServerSocket.Close();
            ServerSocket.Dispose();
        }

        private async Task StartAcceptClient()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                var clientSocket = await ServerSocket.AcceptAsync();
                await DoAcceptClient(clientSocket, _cancellationTokenSource.Token);
            }
        }

        public override ValueTask DoAcceptClient(Socket client, CancellationToken cancellationToken)
        {
            var clientSession = new KcpSession<TId>(client, PacketCodec, DataDispatcher);

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