namespace Hive.Server.Abstractions;

public interface IClusterNodeService
{
    ClusterNodeId NodeId { get; }
    
    ServiceAddress QueryService(ServiceKey serviceKey);
}