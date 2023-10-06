using Hive.Network.Abstractions.Session;

namespace Hive.Network.Abstractions.EventArgs;

public class LoadBalancerInitializedEventArgs<TSession> : System.EventArgs where TSession : ISession
{
    public ILoadBalancer<TSession> LoadBalancer { get; }
    public TSession Session { get; }

    public LoadBalancerInitializedEventArgs(ILoadBalancer<TSession> loadBalancer, TSession session)
    {
        LoadBalancer = loadBalancer;
        Session = session;
    }
}