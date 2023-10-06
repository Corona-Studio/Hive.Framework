using Hive.Common.Shared;

namespace Hive.DataSync.Abstractions.Interfaces
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