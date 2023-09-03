using Hive.DataSynchronizer.Abstraction.Interfaces;

namespace Hive.DataSynchronizer.Abstraction
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