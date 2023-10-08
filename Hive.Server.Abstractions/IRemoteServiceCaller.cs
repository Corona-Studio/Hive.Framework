namespace Hive.Server.Abstractions;

public interface IRemoteServiceCaller
{
    void Call(ServiceAddress serviceAddress, string methodName, Stream serializedArguments, Action<Stream> serializedResult);
    
    Task<Stream> CallAsync(ServiceAddress serviceAddress, string methodName, Stream serializedArguments);
}