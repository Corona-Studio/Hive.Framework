using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;

namespace Hive.Framework.Networking.Quic;

[RequiresPreviewFeatures]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class QuicAcceptor<TId, TSessionId> : AbstractAcceptor<QuicConnection, QuicSession<TId>, TId, TSessionId>
    where TId : unmanaged
    where TSessionId : unmanaged
{
    public QuicAcceptor(IPEndPoint endPoint, IPacketCodec<TId> packetCodec, IDataDispatcher<QuicSession<TId>> dataDispatcher, ISessionManager<TSessionId, QuicSession<TId>> sessionManager, ISessionCreator<QuicSession<TId>, QuicConnection> sessionCreator, X509Certificate2 serverCertificate) : base(endPoint, packetCodec, dataDispatcher, sessionManager, sessionCreator)
    {
        ServerCertificate = serverCertificate;
    }
    
    public QuicListener? QuicListener { get; private set; }

    public override bool IsValid => QuicListener != null;
    public X509Certificate2 ServerCertificate { get; }

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

    public override async Task<bool> SetupAsync(CancellationToken token)
    {
        await InitListener();
        if (QuicListener == null)
            return false;

        return true;
    }

    public override async Task<bool> CloseAsync(CancellationToken token)
    {
        if(QuicListener == null)
            return false;
        
        await QuicListener.DisposeAsync();
        QuicListener = null;
        return true;
    }

    public override async ValueTask<bool> DoOnceAcceptAsync(CancellationToken token)
    {
        if(QuicListener == null)
            return false;
        
        var connection = await QuicListener.AcceptConnectionAsync(token);
        //var stream = await connection.AcceptInboundStreamAsync(token);
        var clientSession = SessionCreator.CreateSession(connection, connection.RemoteEndPoint);

        ClientManager.AddSession(clientSession);
        return true;
    }
}