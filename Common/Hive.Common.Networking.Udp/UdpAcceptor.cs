using System;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Helpers;
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
        public UdpAcceptor(
            IPEndPoint endPoint,
            IPacketCodec<TId> packetCodec,
            Func<IDataDispatcher<UdpSession<TId>>> dataDispatcherProvider,
            IClientManager<TSessionId, UdpSession<TId>> clientManager)
            : base(endPoint, packetCodec, dataDispatcherProvider, clientManager)
        {
        }

        public Socket? Socket { get; private set; }

        public override void Start()
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket.ReceiveBufferSize = UdpSession<int>.DefaultSocketBufferSize;
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
                await Task.Delay(1);
            }
        }

        public override async ValueTask DoAcceptClient(Socket client, CancellationToken cancellationToken)
        {
            if (client.Available <= 0) return;

            var buffer = ArrayPool<byte>.Shared.Rent(1024);
            try
            {
                EndPoint? endPoint = new IPEndPoint(IPAddress.Any, 0);
                var received = client.ReceiveFrom(buffer, ref endPoint);

                if (received == 0) return;

                if (ClientManager.TryGetSession((IPEndPoint)endPoint, out var session))
                {
                    await session!.DataChannel.Writer.WriteAsync(buffer.AsMemory()[..received], cancellationToken);

                    return;
                }

                var clientSession =
                    new UdpSession<TId>(client, (IPEndPoint)endPoint, PacketCodec, DataDispatcherProvider());

                await clientSession.DataChannel.Writer.WriteAsync(buffer.AsMemory()[..received], cancellationToken);

                ClientManager.AddSession(clientSession);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            Stop();
        }
    }
}