using Hive.DataSync.Abstractions.Interfaces;

namespace Hive.DataSync.Abstractions
{
    public interface IDataSynchronizer<in T>
    {
        void Start();
        void Stop();

        void AddSync(T syncObject);
        void RemoveSync(T syncObject);
        void RemoveSync(ushort objectSyncId);

        void PerformSync(ISyncPacket syncPacket);
    }
}