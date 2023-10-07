using System;

namespace Hive.DataSync.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SyncObjectAttribute : Attribute
    {
        public SyncObjectAttribute(ushort objectSyncId)
        {
            ObjectSyncId = objectSyncId;
        }

        public ushort ObjectSyncId { get; }
    }
}