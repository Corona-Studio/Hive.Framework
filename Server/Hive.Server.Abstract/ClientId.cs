namespace Hive.Server.Abstract;

public struct ClientId : IEquatable<ClientId>, IEqualityComparer<ClientId>
{
#if PACKET_ID_LONG
    public long Id;

    public static implicit operator long(SessionId packetId)
    {
        return packetId.Id;
    }

    public static implicit operator SessionId(long packetId)
    {
        return new SessionId() { Id = packetId };
    }
#else
    public int Id;

    public static implicit operator int(ClientId packetId)
    {
        return packetId.Id;
    }

    public static implicit operator ClientId(int packetId)
    {
        return new ClientId() { Id = packetId };
    }
#endif
    public bool Equals(ClientId other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is ClientId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public bool Equals(ClientId x, ClientId y)
    {
        return x.Id == y.Id;
    }

    public int GetHashCode(ClientId obj)
    {
        return obj.Id;
    }
}