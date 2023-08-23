namespace Hive.DataSynchronizer.Abstraction.Interfaces
{
    public interface IUpdateInfo
    {
        string PropertyName { get; }
    }

    public interface IUpdateInfo<out T> : IUpdateInfo
    {
        T NewValue { get; }
    }
}