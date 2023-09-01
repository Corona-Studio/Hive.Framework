namespace Hive.DataSynchronizer.Abstraction.Interfaces
{
    public interface ISyncObject
    {
        ushort ObjectSyncId { get; }

        void PerformUpdate(IUpdateInfo infoBase);
        void NotifyPropertyChanged(string propertyName, IUpdateInfo updateInfo);
    }
}