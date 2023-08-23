using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Helpers;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using Hive.Framework.Networking.Shared.Attributes;

namespace Hive.Framework.Networking.Quic;

#pragma warning disable CA1416 

[RequiresPreviewFeatures]
public sealed class QuicAcceptor<TId, TSessionId> : AbstractAcceptor<QuicConnection, QuicSession<TId>, TId, TSessionId>
    where TId : unmanaged
    where TSessionId : unmanaged
{
    public QuicAcceptor(
        IPEndPoint endPoint,
        X509Certificate2 serverCertificate,
        IPacketCodec<TId> packetCodec,
        Func<IDataDispatcher<QuicSession<TId>>> dataDispatcherProvider,
        IClientManager<TSessionId, QuicSession<TId>> clientManager)
        : base(endPoint, packetCodec, dataDispatcherProvider, clientManager)
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
            ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
            ListenEndPoint = EndPoint,
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                IdleTimeout = TimeSpan.FromMinutes(5),
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                    ServerCertificate = ServerCertificate
                }
            })
        };

        var listener = await QuicListener.ListenAsync(listenerOptions);

        QuicListener = listener;
    }

    [IgnoreQuicException(QuicError.OperationAborted)]
    [IgnoreException(typeof(OperationCanceledException))]
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
        var clientSession = new QuicSession<TId>(client, stream, PacketCodec, DataDispatcherProvider());

        ClientManager.AddSession(clientSession);
    }
}