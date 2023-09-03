using System;
using Hive.DataSynchronizer.Abstraction.Interfaces;
using Hive.Framework.Shared;

namespace Hive.DataSynchronizer.Shared.ObjectSyncPacket
{
    public abstract class AbstractObjectSyncPacket
    {
        public string PropertyName { get; }
        public ushort ObjectSyncId { get; }
        public SyncOptions SyncOptions { get; }

        public AbstractObjectSyncPacket(
            string propertyName,
            ushort objectSyncId,
            SyncOptions syncOptions)
        {
            PropertyName = propertyName;
            ObjectSyncId = objectSyncId;
            SyncOptions = syncOptions;
        }

        public abstract ReadOnlyMemory<byte> Serialize();
    }

    public abstract class AbstractObjectSyncPacket<T> : AbstractObjectSyncPacket, ISyncPacket<T>
    {
        public T NewValue { get; }
        
        protected AbstractObjectSyncPacket(
            ushort objectSyncId,
            string propertyName,
            SyncOptions syncOptions,
            T newValue) : base(propertyName, objectSyncId, syncOptions)
        {
            NewValue = newValue;
        }
    }
}