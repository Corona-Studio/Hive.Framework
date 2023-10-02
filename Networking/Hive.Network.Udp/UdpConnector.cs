using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions.Session;
using Hive.Network.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Udp
{
    public class UdpConnector : IConnector<UdpSession>
    {
        private int _currentSessionId;
        private readonly ILogger<UdpConnector> _logger;
        private readonly IServiceProvider _serviceProvider;
        private ObjectFactory<UdpClientSession> _sessionFactory;
        public UdpConnector(ILogger<UdpConnector> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _sessionFactory = ActivatorUtilities.CreateFactory<UdpClientSession>(new []{typeof(int),
                typeof(Socket), typeof(IPEndPoint)});
        }

        public async ValueTask<UdpSession> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token = default)
        {
            try
            {
                var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                var buffer = new byte[NetworkSetting.PacketBodyOffset+HandShakePacket.Size];
                var segment = new ArraySegment<byte>(buffer, 0, NetworkSetting.PacketBodyOffset+HandShakePacket.Size);
       
                int syn = new Random(DateTimeOffset.Now.Millisecond).Next();
                
                var shakeFirst = new HandShakePacket()
                {
                    syn = syn
                };
                var length = HandShakePacket.Size + NetworkSetting.PacketBodyOffset;
                BitConverter.TryWriteBytes(segment.AsSpan(), length);
                BitConverter.TryWriteBytes(segment.AsSpan().Slice(NetworkSetting.SessionIdOffset),
                    NetworkSetting.HandshakeSessionId);
                
                shakeFirst.WriteTo(segment.AsSpan().Slice(NetworkSetting.PacketBodyOffset));
                
                await socket.SendToAsync(segment, SocketFlags.None, remoteEndPoint);
                
                await socket.ReceiveFromAsync(segment, SocketFlags.None, remoteEndPoint);
                
                var responseFirst = HandShakePacket.ReadFrom(segment.AsSpan().Slice(NetworkSetting.PacketBodyOffset));
                if (!responseFirst.IsResponseOf(shakeFirst))
                {
                    return null;
                }

                var secondShake = responseFirst.Next();
                secondShake.WriteTo(buffer.AsSpan().Slice(NetworkSetting.PacketBodyOffset));
                
                await socket.SendToAsync(segment, SocketFlags.None, remoteEndPoint);
                
                await socket.ReceiveFromAsync(segment, SocketFlags.None, remoteEndPoint);
                var secondResponse = HandShakePacket.ReadFrom(segment.AsSpan().Slice(NetworkSetting.PacketBodyOffset));
                if (!secondResponse.IsResponseOf(secondShake) ||
                    !secondResponse.IsClientFinished())
                {
                    return null;
                }

                var sessionId = secondResponse.SessionId;
                return _sessionFactory.Invoke(_serviceProvider,new object[]
                {
                    (int)sessionId,
                    socket,
                    remoteEndPoint,
                });
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