using Hive.Server.Abstractions;

namespace Hive.Server.Cluster.RSC;

public class CodeGenExecutor : IRemoteServiceCallExecutor
{
    public void OnCall(ServiceAddress serviceAddress, string methodName, Stream serializedArguments, Stream serializedResult)
    {
        
    }
}