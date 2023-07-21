using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Helpers;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;

namespace Hive.Framework.Networking.Quic;

#pragma warning disable CA1416 

[RequiresPreviewFeatures]
public sealed class QuicAcceptor<TId, TSessionId> : AbstractAcceptor<QuicConnection, QuicSession<TId>, TId, TSessionId> where TId : unmanaged
{
    public QuicAcceptor(
        IPEndPoint endPoint,
        X509Certificate2 serverCertificate,
        IPacketCodec<TId> packetCodec,
        IDataDispatcher<QuicSession<TId>> dataDispatcher,
        IClientManager<TSessionId, QuicSession<TId>> clientManager) : base(endPoint, packetCodec, dataDispatcher, clientManager)
    {
        if (!QuicListener.IsSupported)
            throw new NotSupportedException("QUIC is not supported on this platform!");

        ServerCertificate = serverCertificate;
    }

    public QuicListener? QuicListener { get; private set; }
    public X509Certificate2 ServerCertificate { get; }

    public override void Start()
    {
        TaskHelper.ManagedRun(StartAcceptClient, CancellationTokenSource.Token);
    }

    public override void Stop()
    {
        QuicListener?.DisposeAsync().AsTask().Wait();
    }

    private async ValueTask InitListener()
    {
        var listenerOptions = new QuicListenerOptions
        {
            ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http2 },
            ListenEndPoint = EndPoint,
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                    ServerCertificate = ServerCertificate
                },
                IdleTimeout = TimeSpan.FromMinutes(5)
            })
        };

        var listener = await QuicListener.ListenAsync(listenerOptions);

        QuicListener = listener;
    }

    private async Task StartAcceptClient()
    {
        await InitListener();

        if(QuicListener == null)
            throw new InvalidOperationException("QuicListener Init failed!");

        while (!CancellationTokenSource.IsCancellationRequested)
        {
            var connection = await QuicListener.AcceptConnectionAsync(CancellationTokenSource.Token);

            await DoAcceptClient(connection, CancellationTokenSource.Token);
        }
    }

    public override async ValueTask DoAcceptClient(QuicConnection client, CancellationToken cancellationToken)
    {
        var stream = await client.AcceptInboundStreamAsync(cancellationToken);
        var clientSession = new QuicSession<TId>(client, stream, PacketCodec, DataDispatcher);

        ClientManager.AddSession(clientSession);
    }
}