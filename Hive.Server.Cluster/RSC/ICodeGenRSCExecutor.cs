namespace Hive.Server.Cluster.RSC;

public interface ICodeGenRSCExecutor
{
    void Execute(string methodName, Stream serializedArguments, Stream serializedResult);
}