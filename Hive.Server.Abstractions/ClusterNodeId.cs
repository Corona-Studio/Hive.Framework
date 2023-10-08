﻿namespace Hive.Server.Abstractions;

public struct ClusterNodeId : IEquatable<ClusterNodeId>, IEqualityComparer<ClusterNodeId>
{
    public int Id;

    public bool Equals(ClusterNodeId other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is ClusterNodeId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public bool Equals(ClusterNodeId x, ClusterNodeId y)
    {
        return x.Id == y.Id;
    }

    public int GetHashCode(ClusterNodeId obj)
    {
        return obj.Id;
    }
}