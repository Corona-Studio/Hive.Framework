using System;
using Hive.Network.Abstractions;

namespace Hive.Network.Udp
{
    public struct HandShakePacket
    {
        public static int Size => 12 + SessionId.Size;
        public int syn;
        public int state;
        public SessionId SessionId;
        public void WriteTo(Span<byte> span)
        {
            BitConverter.TryWriteBytes(span.Slice(0), 0x1140403);
            BitConverter.TryWriteBytes(span.Slice(4), syn);
            BitConverter.TryWriteBytes(span.Slice(8), state);
            BitConverter.TryWriteBytes(span.Slice(12), SessionId);
        }
        
        public bool IsHeaderValid(Span<byte> span)
        {
            return BitConverter.ToInt32(span.Slice(4)) == 0x1140403;
        }
        
        public bool IsServerFinished()
        {
            return state == 2;
        }
        
        public bool IsClientFinished()
        {
            return state == 3;
        }
        
        public HandShakePacket CreateFinal(SessionId sessionId)
        {
            return new HandShakePacket
            {
                syn = syn + 1,
                state = 3,
                SessionId = sessionId,
            };
        }
        
        public HandShakePacket Next()
        {
            return new HandShakePacket
            {
                syn = syn + 1,
                state = state + 1
            };
        }
        
        public bool IsResponseOf(HandShakePacket packet)
        {
            return syn == packet.syn + 1 && state == packet.state + 1;
        }
        
        public static HandShakePacket ReadFrom(ReadOnlySpan<byte> span)
        {
            return new HandShakePacket
            {
                syn = BitConverter.ToInt32(span.Slice(4)),
                state = BitConverter.ToInt32(span.Slice(8)),
                SessionId = SessionId.FromSpan(span.Slice(12))
            };
        }
    }
}