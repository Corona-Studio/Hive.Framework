using System.Net;
using System.Net.Quic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hive.Network.Shared.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hive.Network.Quic;

[RequiresPreviewFeatures]
[SupportedOSPlatform(nameof(OSPlatform.Windows))]
[SupportedOSPlatform(nameof(OSPlatform.Linux))]
[SupportedOSPlatform(nameof(OSPlatform.OSX))]
public sealed class QuicAcceptor : AbstractAcceptor<QuicSession>
{
    private readonly QuicListenerOptions _listenerOptions;
    private readonly ObjectFactory<QuicSession> _sessionFactory;
    private QuicListener? _listener;

    public QuicAcceptor(
        IOptions<QuicAcceptorOptions> quicOptions,
        IServiceProvider serviceProvider,
        ILogger<QuicAcceptor> logger)
        : base(serviceProvider, logger)
    {
        _listenerOptions = quicOptions.Value.QuicListenerOptions ??
                           throw new NullReferenceException(nameof(quicOptions.Value.QuicListenerOptions));
        _sessionFactory =
            ActivatorUtilities.CreateFactory<QuicSession>(new[]
                { typeof(int), typeof(QuicConnection), typeof(QuicStream) });
    }

    public override IPEndPoint? EndPoint => _listener?.LocalEndPoint;
    public override bool IsValid => _listener != null;

    private async ValueTask InitListener(IPEndPoint listenEndPoint)
    {
        if (_listenerOptions.ListenEndPoint.Equals(QuicNetworkSettings.FallBackEndPoint))
            _listenerOptions.ListenEndPoint = listenEndPoint;

        var listener = await QuicListener.ListenAsync(_listenerOptions);
        _listener = listener;
    }

    public override async Task<bool> SetupAsync(IPEndPoint listenEndPoint, CancellationToken token)
    {
        if (_listener == null)
            await InitListener(listenEndPoint);

        if (_listener == null)
            throw new NullReferenceException("ServerSocket is null and InitSocket failed.");

        return true;
    }

    public override async Task<bool> CloseAsync(CancellationToken token)
    {
        if (_listener != null)
        {
            await _listener.DisposeAsync();
            _listener = null;
        }

        return true;
    }


    public override async ValueTask<bool> DoOnceAcceptAsync(CancellationToken token)
    {
        if (_listener == null)
            return false;

        var conn = await _listener.AcceptConnectionAsync(token);
        var stream = await conn.AcceptInboundStreamAsync(token);

        CreateSession(conn, stream);

        return true;
    }

    private void CreateSession(QuicConnection conn, QuicStream stream)
    {
        var sessionId = GetNextSessionId();
        var clientSession = _sessionFactory.Invoke(ServiceProvider, new object[] { sessionId, conn, stream });
        clientSession.OnQuicError += OnQuicError;
        FireOnSessionCreate(clientSession);
    }

    private void OnQuicError(object sender, QuicError e)
    {
        if (sender is QuicSession session)
        {
            Logger.LogDebug("Session {sessionId} QUIC error: {quicError}", session.Id, e);
            session.Close();
            FireOnSessionClosed(session);
        }
    }

    public override void Dispose()
    {
        CloseAsync(CancellationToken.None).Wait();
    }
}