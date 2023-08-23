using Hive.DataSynchronizer.Abstraction.Interfaces;

namespace Hive.DataSynchronizer.Abstraction
{
    public interface IDataSynchronizer
    {
        void Start();
        void Stop();

        void AddSync(IDataSynchronizationObject synchronizationObject);
        void RemoveSync(IDataSynchronizationObject synchronizationObject);
        void RemoveSync(ushort objectSyncId);
    }
}