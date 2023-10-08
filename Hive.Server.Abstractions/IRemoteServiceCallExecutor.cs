namespace Hive.Server.Abstractions;

public interface IRemoteServiceCallExecutor
{
    void OnCall(ServiceAddress serviceAddress, string methodName, Stream serializedArguments, Stream serializedResult);
}