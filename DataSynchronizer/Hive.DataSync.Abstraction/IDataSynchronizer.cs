using Hive.DataSync.Abstraction.Interfaces;

namespace Hive.DataSync.Abstraction
{
    public interface IDataSynchronizer
    {
        void Start();
        void Stop();

        void AddSync(ISyncObject synchronizationObject);
        void RemoveSync(ISyncObject synchronizationObject);
        void RemoveSync(ushort objectSyncId);

        void PerformSync(ISyncPacket syncPacket);
    }
}