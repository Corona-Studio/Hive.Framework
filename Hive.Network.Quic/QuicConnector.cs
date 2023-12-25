using System.Net;
using System.Net.Quic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hive.Network.Quic;

[RequiresPreviewFeatures]
[SupportedOSPlatform(nameof(OSPlatform.Windows))]
[SupportedOSPlatform(nameof(OSPlatform.Linux))]
[SupportedOSPlatform(nameof(OSPlatform.OSX))]
public class QuicConnector : IConnector<QuicSession>
{
    private readonly QuicConnectorOptions _connectorOptions;

    private readonly ILogger<QuicConnector> _logger;
    private readonly IServiceProvider _serviceProvider;
    private int _currentSessionId;

    public QuicConnector(
        IOptions<QuicConnectorOptions> connectorOptions,
        ILogger<QuicConnector> logger,
        IServiceProvider serviceProvider)
    {
        if (connectorOptions.Value.ClientConnectionOptions == null)
            throw new ArgumentNullException(nameof(connectorOptions.Value.ClientConnectionOptions));

        _connectorOptions = connectorOptions.Value;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async ValueTask<QuicSession?> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token = default)
    {
        if (_connectorOptions.ClientConnectionOptions!.RemoteEndPoint.Equals(QuicNetworkSettings.FallBackEndPoint))
            _connectorOptions.ClientConnectionOptions!.RemoteEndPoint = remoteEndPoint;

        try
        {
            var conn = await QuicConnection.ConnectAsync(_connectorOptions.ClientConnectionOptions, token);
            var stream = await conn.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, token);

            return ActivatorUtilities.CreateInstance<QuicSession>(
                _serviceProvider,
                GetNextSessionId(),
                conn,
                stream);
        }
        catch (Exception e)
        {
            _logger.LogConnectFailed(e, remoteEndPoint);
            throw;
        }
    }

    public int GetNextSessionId()
    {
        return Interlocked.Increment(ref _currentSessionId);
    }
}

[RequiresPreviewFeatures]
internal static partial class QuicConnectorLoggers
{
    [LoggerMessage(LogLevel.Error, "{ex} Connect to {remote} failed")]
    public static partial void LogConnectFailed(this ILogger logger, Exception ex, IPEndPoint remote);
}