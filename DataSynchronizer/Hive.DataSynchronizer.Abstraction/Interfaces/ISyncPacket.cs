using Hive.Framework.Shared;

namespace Hive.DataSynchronizer.Abstraction.Interfaces
{
    public interface ISyncPacket
    {
        string PropertyName { get; }
        ushort ObjectSyncId { get; }
        SyncOptions SyncOptions { get; }
    }

    public interface ISyncPacket<out T> : ISyncPacket
    {
        T NewValue { get; }
    }
}