using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions.Session;
using Hive.Network.Shared.HandShake;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Udp
{
    public class UdpConnector : IConnector<UdpSession>
    {
        private readonly ILogger<UdpConnector> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ObjectFactory<UdpClientSession> _sessionFactory;

        public UdpConnector(
            ILogger<UdpConnector> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _sessionFactory = ActivatorUtilities.CreateFactory<UdpClientSession>(new[]
            {
                typeof(int),
                typeof(Socket), typeof(IPEndPoint)
            });
        }

        public async ValueTask<UdpSession?> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token = default)
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
                    remoteEndPoint
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