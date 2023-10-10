using System.Net;

namespace Hive.Server.Abstractions;

public struct ClusterNodeInfo: IEquatable<ClusterNodeInfo>
{
    public readonly ClusterNodeId NodeId { get; }
    private readonly IPEndPoint NodeEndPoint { get; }
    public readonly string MachineId { get; }
    public readonly ServiceKey[] ServiceKeys { get; }
    public DateTime LastHeartBeatTime { get; set; }

    public ClusterNodeInfo(ClusterNodeId nodeId, IPEndPoint nodeEndPoint, string machineId, ServiceKey[] serviceKeys)
    {
        NodeId = nodeId;
        NodeEndPoint = nodeEndPoint;
        MachineId = machineId;
        ServiceKeys = serviceKeys;
    }

    public bool Equals(ClusterNodeInfo other)
    {
        return NodeId.Equals(other.NodeId);
    }

    public override bool Equals(object? obj)
    {
        return obj is ClusterNodeInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return NodeId.GetHashCode();
    }

    public static bool operator ==(ClusterNodeInfo left, ClusterNodeInfo right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ClusterNodeInfo left, ClusterNodeInfo right)
    {
        return !(left == right);
    }
}