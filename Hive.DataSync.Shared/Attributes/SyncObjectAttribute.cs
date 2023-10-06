using System;

namespace Hive.DataSync.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SyncObjectAttribute : Attribute
    {
        public ushort ObjectSyncId { get; }

        public SyncObjectAttribute(ushort objectSyncId)
        {
            ObjectSyncId = objectSyncId;
        }
    }
}