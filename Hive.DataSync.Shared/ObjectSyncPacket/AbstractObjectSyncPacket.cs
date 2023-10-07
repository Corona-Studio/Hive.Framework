using System;
using Hive.Common.Shared;
using Hive.DataSync.Abstractions.Interfaces;

namespace Hive.DataSync.Shared.ObjectSyncPacket
{
    public abstract class AbstractObjectSyncPacket
    {
        public AbstractObjectSyncPacket(
            string propertyName,
            ushort objectSyncId,
            SyncOptions syncOptions)
        {
            PropertyName = propertyName;
            ObjectSyncId = objectSyncId;
            SyncOptions = syncOptions;
        }

        public string PropertyName { get; }
        public ushort ObjectSyncId { get; }
        public SyncOptions SyncOptions { get; }

        public abstract ReadOnlyMemory<byte> Serialize();
    }

    public abstract class AbstractObjectSyncPacket<T> : AbstractObjectSyncPacket, ISyncPacket<T>
    {
        protected AbstractObjectSyncPacket(
            ushort objectSyncId,
            string propertyName,
            SyncOptions syncOptions,
            T newValue) : base(propertyName, objectSyncId, syncOptions)
        {
            NewValue = newValue;
        }

        public T NewValue { get; }
    }
}