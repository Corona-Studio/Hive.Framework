using System;
using System.Collections.Generic;

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
        return new SessionId { Id = packetId };
    }

    public static SessionId FromSpan(ReadOnlySpan<byte> span)
    {
        return new SessionId
        {
            Id = BitConverter.ToInt32(span)
        };
    }
#endif
    public readonly bool Equals(SessionId other)
    {
        return Id == other.Id;
    }

    public readonly override bool Equals(object? obj)
    {
        return obj is SessionId other && Equals(other);
    }

    public readonly override int GetHashCode()
    {
        return Id;
    }

    public readonly bool Equals(SessionId x, SessionId y)
    {
        return x.Id == y.Id;
    }

    public readonly int GetHashCode(SessionId obj)
    {
        return obj.Id;
    }

    public readonly override string ToString() => Id.ToString();
}