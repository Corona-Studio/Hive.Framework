using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Tcp;

public class TcpConnector : IConnector<TcpSession>
{
    private readonly ILogger<TcpConnector> _logger;
    private readonly IServiceProvider _serviceProvider;
    private int _currentSessionId;

    public TcpConnector(
        ILogger<TcpConnector> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async ValueTask<TcpSession?> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token = default)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await using var registration = token.Register(() =>
        {
            try { socket.Close(); } catch { /* Ignore */ }
        });

        try
        {
            await socket.ConnectAsync(remoteEndPoint);

            return ActivatorUtilities.CreateInstance<TcpSession>(_serviceProvider, GetNextSessionId(), socket);
        }
        catch (ObjectDisposedException) when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException(token);
        }
        catch (SocketException e)
        {
            _logger.LogConnectFailed(e, remoteEndPoint);
            return null;
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

internal static partial class TcpConnectorLoggers
{
    [LoggerMessage(LogLevel.Error, "[TCP_CONN] Connect to {RemoteEndPoint} failed")]
    public static partial void LogConnectFailed(this ILogger logger, Exception ex, IPEndPoint remoteEndPoint);
}