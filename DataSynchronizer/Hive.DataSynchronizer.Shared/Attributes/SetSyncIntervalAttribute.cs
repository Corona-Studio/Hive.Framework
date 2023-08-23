using System;

namespace Hive.DataSynchronizer.Shared.Attributes
{
    public sealed class SetSyncIntervalAttribute : Attribute
    {
        public TimeSpan SyncInterval { get; }

        public SetSyncIntervalAttribute(int syncInterval)
        {
            SyncInterval = TimeSpan.FromSeconds(syncInterval);
        }
    }
}