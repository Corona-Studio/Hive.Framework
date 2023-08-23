using System;

namespace Hive.DataSynchronizer.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DataSynchronizationObjectAttribute : Attribute
    {
        public ushort ObjectSyncId { get; }

        public DataSynchronizationObjectAttribute(ushort objectSyncId)
        {
            ObjectSyncId = objectSyncId;
        }
    }
}