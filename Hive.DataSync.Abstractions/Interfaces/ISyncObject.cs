using System.Collections.Generic;

namespace Hive.DataSync.Abstractions.Interfaces
{
    public interface ISyncObject
    {
        ushort ObjectSyncId { get; }

        void PerformUpdate(ISyncPacket infoBase);
        IEnumerable<ISyncPacket> GetPendingChanges();
        void NotifyPropertyChanged(string propertyName, ISyncPacket syncPacket);
    }
}