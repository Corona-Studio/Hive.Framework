using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System;
using Hive.Network.Shared.HandShake;
using Hive.Network.Abstractions.Session;

namespace Hive.Network.Kcp
{
    public class KcpConnector : IConnector<KcpSession>
    {
        private readonly ILogger<KcpConnector> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ObjectFactory<KcpClientSession> _sessionFactory;

        public KcpConnector(
            ILogger<KcpConnector> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _sessionFactory = ActivatorUtilities.CreateFactory<KcpClientSession>(new[]{typeof(int),
                typeof(Socket), typeof(IPEndPoint)});
        }

        public async ValueTask<KcpSession?> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token = default)
        {
            try
            {
                var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                var shakeResult = await socket.HandShakeWith(remoteEndPoint);

                if (!shakeResult.HasValue) return null;

                var sessionId = shakeResult.Value.SessionId;
                return _sessionFactory.Invoke(_serviceProvider, new object[]
                {
                    (int)sessionId,
                    socket,
                    remoteEndPoint,
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Connect to {0} failed", remoteEndPoint);
                throw;
            }
        }
    }
}