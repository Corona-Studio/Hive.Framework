using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hive.Framework.Networking.Quic;
using Hive.Network.Shared.Session;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Hive.Network.Quic;

[RequiresPreviewFeatures]
[SupportedOSPlatform(nameof(OSPlatform.Windows))]
[SupportedOSPlatform(nameof(OSPlatform.Linux))]
[SupportedOSPlatform(nameof(OSPlatform.OSX))]
public sealed class QuicAcceptor : AbstractAcceptor<QuicSession>
{
    private QuicListener? _listener;
    private readonly X509Certificate2 _certificate;

    public override IPEndPoint? EndPoint => _listener?.LocalEndPoint;
    public override bool IsValid => _listener != null;

    private readonly ObjectFactory<QuicSession> _sessionFactory;

    public QuicAcceptor(
        X509Certificate2 serverCertificate,
        IServiceProvider serviceProvider,
        ILogger<QuicAcceptor> logger)
        : base(serviceProvider, logger)
    {
        _certificate = serverCertificate;
        _sessionFactory = ActivatorUtilities.CreateFactory<QuicSession>(new[] { typeof(int), typeof(QuicConnection), typeof(QuicStream) });
    }

    private async ValueTask InitListener(IPEndPoint listenEndPoint)
    {
        var listenerOptions = new QuicListenerOptions
        {
            ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
            ListenEndPoint = listenEndPoint,
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                IdleTimeout = TimeSpan.FromMinutes(5),
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                    ServerCertificate = _certificate
                }
            })
        };

        var listener = await QuicListener.ListenAsync(listenerOptions);
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