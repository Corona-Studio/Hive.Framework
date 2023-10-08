using System.Net;

namespace Hive.Server.Abstractions;

public record struct ClusterNodeInfo(ClusterNodeId NodeId, IPEndPoint NodeEndPoint, string MachineId);