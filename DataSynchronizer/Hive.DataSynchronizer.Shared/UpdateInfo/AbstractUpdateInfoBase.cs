using System;
using Hive.DataSynchronizer.Abstraction.Interfaces;
using Hive.Framework.Codec.Abstractions;

namespace Hive.DataSynchronizer.Shared.UpdateInfo
{
    public abstract class AbstractUpdateInfoBase
    {
        public string PropertyName { get; }
        public ushort ObjectSyncId { get; }

        public AbstractUpdateInfoBase(string propertyName, ushort objectSyncId)
        {
            PropertyName = propertyName;
            ObjectSyncId = objectSyncId;
        }

        public abstract ReadOnlyMemory<byte> Serialize<TId>(IPacketCodec<TId> codec) where TId : unmanaged;

        public abstract AbstractUpdateInfoBase Deserialize(ReadOnlyMemory<byte> memory);
    }

    public abstract class AbstractUpdateInfoBase<T> : AbstractUpdateInfoBase, IUpdateInfo<T>
    {
        public T NewValue { get; }

        protected AbstractUpdateInfoBase(ushort objectSyncId, string propertyName, T newValue) : base(propertyName, objectSyncId)
        {
            NewValue = newValue;
        }
    }
}