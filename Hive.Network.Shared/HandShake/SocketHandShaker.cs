using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Hive.Network.Shared.HandShake
{
    public static class SocketHandShaker
    {
        public static async ValueTask<HandShakePacket?> HandShakeWith(this Socket socket, EndPoint remoteEndPoint)
        {
            var buffer = new byte[NetworkSettings.PacketBodyOffset + HandShakePacket.Size];
            var segment = new ArraySegment<byte>(buffer, 0, NetworkSettings.PacketBodyOffset + HandShakePacket.Size);
            var syn = new Random(DateTimeOffset.Now.Millisecond).Next();

            var shakeFirst = new HandShakePacket
            {
                Syn = syn
            };
            var length = HandShakePacket.Size + NetworkSettings.PacketBodyOffset;
            BitConverter.TryWriteBytes(segment.AsSpan(), length);
            BitConverter.TryWriteBytes(segment.AsSpan()[NetworkSettings.SessionIdOffset..], NetworkSettings.HandshakeSessionId);

            shakeFirst.WriteTo(segment.AsSpan()[NetworkSettings.PacketBodyOffset..]);

            await socket.SendToAsync(segment, SocketFlags.None, remoteEndPoint);
            await socket.ReceiveFromAsync(segment, SocketFlags.None, remoteEndPoint);

            var responseFirst = HandShakePacket.ReadFrom(segment.AsSpan()[NetworkSettings.PacketBodyOffset..]);
            if (!responseFirst.IsResponseOf(shakeFirst))
            {
                return null;
            }

            var secondShake = responseFirst.Next();
            secondShake.WriteTo(buffer.AsSpan()[NetworkSettings.PacketBodyOffset..]);

            await socket.SendToAsync(segment, SocketFlags.None, remoteEndPoint);
            await socket.ReceiveFromAsync(segment, SocketFlags.None, remoteEndPoint);

            var secondResponse = HandShakePacket.ReadFrom(segment.AsSpan()[NetworkSettings.PacketBodyOffset..]);
            if (!secondResponse.IsResponseOf(secondShake) ||
                !secondResponse.IsClientFinished())
            {
                return null;
            }

            return secondResponse;
        }
    }
}