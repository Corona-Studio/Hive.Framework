using System;
using Hive.Network.Abstractions;

namespace Hive.Network.Shared.HandShake
{
    public struct HandShakePacket
    {
        public static int Size => 12 + SessionId.Size;
        public int Syn;
        public int State;
        public SessionId SessionId;

        public readonly void WriteTo(Span<byte> span)
        {
            BitConverter.TryWriteBytes(span[..], 0x1140403);
            BitConverter.TryWriteBytes(span[4..], Syn);
            BitConverter.TryWriteBytes(span[8..], State);
            BitConverter.TryWriteBytes(span[12..], SessionId);
        }
        
        public readonly bool IsHeaderValid(Span<byte> span)
        {
            return BitConverter.ToInt32(span[4..]) == 0x1140403;
        }
        
        public readonly bool IsServerFinished()
        {
            return State == 2;
        }
        
        public readonly bool IsClientFinished()
        {
            return State == 3;
        }

        public readonly bool IsResponseOf(HandShakePacket packet)
        {
            return Syn == packet.Syn + 1 && State == packet.State + 1;
        }

        public HandShakePacket CreateFinal(SessionId sessionId)
        {
            return new HandShakePacket
            {
                Syn = Syn + 1,
                State = 3,
                SessionId = sessionId,
            };
        }
        
        public HandShakePacket Next()
        {
            return new HandShakePacket
            {
                Syn = Syn + 1,
                State = State + 1
            };
        }
        
        public static HandShakePacket ReadFrom(ReadOnlySpan<byte> span)
        {
            return new HandShakePacket
            {
                Syn = BitConverter.ToInt32(span[4..]),
                State = BitConverter.ToInt32(span[8..]),
                SessionId = SessionId.FromSpan(span[12..])
            };
        }
    }
}