using System;
using System.Text;
using Hive.Common.Shared;
using Hive.Common.Shared.Helpers;

namespace Hive.DataSync.Shared.ObjectSyncPacket
{
    public class UInt16SyncPacket : AbstractObjectSyncPacket<ushort>
    {
        public UInt16SyncPacket(
            ushort objectSyncId,
            string propertyName,
            SyncOptions syncOptions,
            ushort newValue) : base(objectSyncId, propertyName, syncOptions, newValue)
        {
        }

        public override ReadOnlyMemory<byte> Serialize()
        {
            var propertyNameMemory = Encoding.UTF8.GetBytes(PropertyName).AsSpan();

            // [OBJ_SYNC_ID (2) | NEW_VAL | PROPERTY_NAME]
            var totalLength = sizeof(ushort) + sizeof(ushort) + propertyNameMemory.Length;
            var result = new Memory<byte>(new byte[totalLength]);

            var index = 0;

            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(ushort)),
                ObjectSyncId);
            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(ushort)),
                NewValue);
            propertyNameMemory
                .CopyTo(result.Span.SliceAndIncrement(ref index, propertyNameMemory.Length));

            return result;
        }

        public static UInt16SyncPacket Deserialize(ReadOnlyMemory<byte> memory)
        {
            var index = 0;
            var objectSyncId = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var newValue = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var propertyName = Encoding.UTF8.GetString(memory.Span[index..]);

            return new UInt16SyncPacket(objectSyncId, propertyName, SyncOptions.None, newValue);
        }
    }
}