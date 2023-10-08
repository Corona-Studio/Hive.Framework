using System.Net;

namespace Hive.Server.Cluster;

public struct NodeAgent
{
    public string NodeName;
    public IPEndPoint NodeEndPoint;
    public ServiceAgent[] Services;
}