using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Tcp
{
    public class TcpConnector : IConnector<TcpSession>
    {
        private int _currentSessionId;
        private readonly ILogger<TcpConnector> _logger;
        private readonly IServiceProvider _serviceProvider;

        public TcpConnector(
            ILogger<TcpConnector> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async ValueTask<TcpSession?> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token = default)
        {
            try
            {
                var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(remoteEndPoint);

                return ActivatorUtilities.CreateInstance<TcpSession>(_serviceProvider, GetNextSessionId(), socket);
            }
            catch (Exception e)
            {
                _logger.LogError(e,"Connect to {0} failed", remoteEndPoint);
                throw;
            }
        }
        
        public int GetNextSessionId()
        {
            return Interlocked.Increment(ref _currentSessionId);
        }
    }
}