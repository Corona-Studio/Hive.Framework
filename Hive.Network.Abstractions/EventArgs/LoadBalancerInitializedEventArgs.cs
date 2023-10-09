using Hive.Network.Abstractions.GatewayServer;
using Hive.Network.Abstractions.Session;

namespace Hive.Network.Abstractions.EventArgs;

public class LoadBalancerInitializedEventArgs : System.EventArgs
{
    public LoadBalancerInitializedEventArgs(ILoadBalancer loadBalancer, ISession session)
    {
        LoadBalancer = loadBalancer;
        Session = session;
    }

    public ILoadBalancer LoadBalancer { get; }
    public ISession Session { get; }
}