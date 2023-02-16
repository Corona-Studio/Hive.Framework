using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Helpers;
using System.Net;
using System.Net.Quic;
using System.Runtime.Versioning;

namespace Hive.Framework.Networking.Quic;

#pragma warning disable CA1416 

[RequiresPreviewFeatures]
public class QuicAcceptor<TId> : AbstractAcceptor<QuicConnection, QuicSession<TId>, TId>
{
    public QuicAcceptor(IPEndPoint endPoint, IEncoder<TId> encoder, IDecoder<TId> decoder, IDataDispatcher<QuicSession<TId>> dataDispatcher) : base(endPoint, encoder, decoder, dataDispatcher)
    {
        if (!QuicListener.IsSupported)
            throw new NotSupportedException("QUIC is not supported on this platform!");
    }

    public QuicListener? QuicListener { get; private set; }
    
    public override void Start()
    {
        TaskHelper.ManagedRun(StartAcceptClient, _cancellationTokenSource.Token);
    }

    public override void Stop()
    {
        if (QuicListener == null) return;

        Task.Run(async () =>
        {
            await QuicListener.DisposeAsync();
        });
    }

    private async ValueTask InitListener()
    {
        var serverConnectionOptions = new QuicServerConnectionOptions
        {
            DefaultStreamErrorCode = 0x0A,
            DefaultCloseErrorCode = 0x0B
        };

        var listener = await QuicListener.ListenAsync(new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(serverConnectionOptions)
        });

        QuicListener = listener;
    }

    private async Task StartAcceptClient()
    {
        await InitListener();

        if(QuicListener == null)
            throw new InvalidOperationException("QuicListener Init failed!");

        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            var connection = await QuicListener.AcceptConnectionAsync();

            await DoAcceptClient(connection, _cancellationTokenSource.Token);
        }
    }

    public override async ValueTask DoAcceptClient(QuicConnection client, CancellationToken cancellationToken)
    {
        var stream = await client.AcceptInboundStreamAsync(cancellationToken);
        var clientSession = new QuicSession<TId>(client, stream, Encoder, Decoder, DataDispatcher);
    }
}