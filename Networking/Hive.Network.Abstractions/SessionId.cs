using System;
using System.Collections.Generic;
using Hive.Framework.Codec.Abstractions;

namespace Hive.Network.Abstractions;

public struct SessionId : IEquatable<SessionId>, IEqualityComparer<SessionId>
{
#if PACKET_ID_INT
    public int Id;
    public static int Size => sizeof(int);
    public static implicit operator int(SessionId packetId)
    {
        return packetId.Id;
    }

    public static implicit operator SessionId(int packetId)
    {
        return new SessionId() { Id = packetId };
    }
    
    public static SessionId FromSpan(ReadOnlySpan<byte> span)
    {
        return new SessionId
        {
            Id = BitConverter.ToInt32(span)
        };
    }
#endif
    public bool Equals(SessionId other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is SessionId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public bool Equals(SessionId x, SessionId y)
    {
        return x.Id == y.Id;
    }

    public int GetHashCode(SessionId obj)
    {
        return obj.Id;
    }
}