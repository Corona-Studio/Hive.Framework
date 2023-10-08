using System.Net;

namespace Hive.Server.Abstractions;

public struct ServiceAddress
{
    public string ServiceName;
    public ClusterNodeInfo NodeInfo;
    
    public bool IsInSameNode(ClusterNodeId nodeId)
    {
        return NodeInfo.NodeId.Equals(nodeId);
    }
    
    public bool IsInSameMachine(ClusterNodeInfo nodeInfo)
    {
        if (nodeInfo.Equals(NodeInfo))
        {
            return true;
        }
        
        return nodeInfo.MachineId == NodeInfo.MachineId;
    }
}