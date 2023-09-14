using System.Text;
using System;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.DataSync.Shared.ObjectSyncPacket
{
    public class Int64SyncPacket : AbstractObjectSyncPacket<long>
    {
        public Int64SyncPacket(
            ushort objectSyncId,
            string propertyName,
            SyncOptions syncOptions,
            long newValue) : base(objectSyncId, propertyName, syncOptions, newValue)
        {
        }

        public override ReadOnlyMemory<byte> Serialize()
        {
            var propertyNameMemory = Encoding.UTF8.GetBytes(PropertyName).AsSpan();

            // [OBJ_SYNC_ID (2) | NEW_VAL | PROPERTY_NAME]
            var totalLength = sizeof(ushort) + sizeof(long) + propertyNameMemory.Length;
            var result = new Memory<byte>(new byte[totalLength]);

            var index = 0;

            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(ushort)),
                ObjectSyncId);
            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(long)),
                NewValue);
            propertyNameMemory
                .CopyTo(result.Span.SliceAndIncrement(ref index, propertyNameMemory.Length));

            return result;
        }

        public static Int64SyncPacket Deserialize(ReadOnlyMemory<byte> memory)
        {
            var index = 0;
            var objectSyncId = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var newValue = BitConverter.ToInt64(memory.Span.SliceAndIncrement(ref index, sizeof(long)));
            var propertyName = Encoding.UTF8.GetString(memory.Span[index..]);

            return new Int64SyncPacket(objectSyncId, propertyName, SyncOptions.None, newValue);
        }
    }
}