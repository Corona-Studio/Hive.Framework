namespace Hive.Server.Actor;

public interface ITestActorService
{
    void UseCase();

    Task<int> TestRequest(int param);
}