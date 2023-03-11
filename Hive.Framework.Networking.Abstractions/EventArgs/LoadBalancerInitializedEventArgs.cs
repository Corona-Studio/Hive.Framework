namespace Hive.Framework.Networking.Abstractions.EventArgs;

public class LoadBalancerInitializedEventArgs<TSession> : System.EventArgs where TSession : ISession<TSession>
{
    public ILoadBalancer<TSession> LoadBalancer { get; }
    public TSession Session { get; }

    public LoadBalancerInitializedEventArgs(ILoadBalancer<TSession> loadBalancer, TSession session)
    {
        LoadBalancer = loadBalancer;
        Session = session;
    }
}