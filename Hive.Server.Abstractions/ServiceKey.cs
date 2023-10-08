namespace Hive.Server.Abstractions;

public struct ServiceKey : IEquatable<ServiceKey>, IEqualityComparer<ServiceKey>
{
    public string ServiceName;

    public bool Equals(ServiceKey other)
    {
        return ServiceName == other.ServiceName;
    }

    public override bool Equals(object? obj)
    {
        return obj is ServiceKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ServiceName.GetHashCode();
    }

    public bool Equals(ServiceKey x, ServiceKey y)
    {
        return x.ServiceName == y.ServiceName;
    }

    public int GetHashCode(ServiceKey obj)
    {
        return obj.ServiceName.GetHashCode();
    }
}