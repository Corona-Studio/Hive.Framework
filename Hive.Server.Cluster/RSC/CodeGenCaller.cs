using Hive.Server.Abstractions;

namespace Hive.Server.Cluster.RSC;

public class CodeGenCaller : IRemoteServiceCaller
{
    public void Call(ServiceAddress serviceAddress, string methodName, Stream serializedArguments, Action<Stream> serializedResult)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> CallAsync(ServiceAddress serviceAddress, string methodName, Stream serializedArguments)
    {
        throw new NotImplementedException();
    }
}