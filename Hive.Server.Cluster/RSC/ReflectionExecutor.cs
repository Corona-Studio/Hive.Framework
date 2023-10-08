using Hive.Server.Abstractions;

namespace Hive.Server.Cluster.RSC;

public class ReflectionExecutor: IRemoteServiceCallExecutor
{
    private IServiceProvider _serviceProvider;

    public ReflectionExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private Type GetServiceType(ServiceAddress serviceAddress)
    {
        return typeof(object);
    }
    
    public void OnCall(ServiceAddress serviceAddress, string methodName, Stream serializedArguments, Stream serializedResult)
    {
        var targetServiceType = GetServiceType(serviceAddress);
        var service = _serviceProvider.GetService(targetServiceType);
        
        // todo use reflection to call method
    }
}