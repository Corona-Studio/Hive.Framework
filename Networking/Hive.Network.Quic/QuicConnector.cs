using Hive.Framework.Networking.Quic;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

namespace Hive.Network.Quic;

[RequiresPreviewFeatures]
[SupportedOSPlatform(nameof(OSPlatform.Windows))]
[SupportedOSPlatform(nameof(OSPlatform.Linux))]
[SupportedOSPlatform(nameof(OSPlatform.OSX))]
public class QuicConnector : IConnector<QuicSession>
{
    private int _currentSessionId;
    private readonly ILogger<QuicConnector> _logger;
    private readonly IServiceProvider _serviceProvider;

    public QuicConnector(
        ILogger<QuicConnector> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async ValueTask<QuicSession?> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token = default)
    {
        try
        {
            var clientConnectionOptions = new QuicClientConnectionOptions
            {
                RemoteEndPoint = remoteEndPoint,
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                IdleTimeout = TimeSpan.FromMinutes(5),
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                    RemoteCertificateValidationCallback = (_, _, _, _) => true
                }
            };

            var conn = await QuicConnection.ConnectAsync(clientConnectionOptions, token);
            var stream = await conn.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, token);

            return ActivatorUtilities.CreateInstance<QuicSession>(
                _serviceProvider,
                GetNextSessionId(),
                conn,
                stream);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Connect to {0} failed", remoteEndPoint);
            throw;
        }
    }

    public int GetNextSessionId()
    {
        return Interlocked.Increment(ref _currentSessionId);
    }
}