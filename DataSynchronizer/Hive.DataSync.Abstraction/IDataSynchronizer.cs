using Hive.DataSync.Abstraction.Interfaces;

namespace Hive.DataSync.Abstraction
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