using System.Text;
using System;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.DataSync.Shared.ObjectSyncPacket
{
    public class StringSyncPacket : AbstractObjectSyncPacket<string>
    {
        public StringSyncPacket(
            ushort objectSyncId,
            string propertyName,
            SyncOptions syncOptions,
            string newValue) : base(objectSyncId, propertyName, syncOptions, newValue)
        {
        }

        public override ReadOnlyMemory<byte> Serialize()
        {
            var newValueMemory = Encoding.UTF8.GetBytes(NewValue).AsSpan();
            var propertyNameMemory = Encoding.UTF8.GetBytes(PropertyName).AsSpan();

            // [OBJ_SYNC_ID (2) | NEW_VAL_LENGTH (2) | NEW_VAL | PROPERTY_NAME]
            var totalLength = sizeof(ushort) + sizeof(ushort) + newValueMemory.Length + propertyNameMemory.Length;
            var result = new Memory<byte>(new byte[totalLength]);

            var index = 0;

            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(ushort)),
                ObjectSyncId);
            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(ushort)),
                (ushort)newValueMemory.Length);
            newValueMemory
                .CopyTo(result.Span.SliceAndIncrement(ref index, newValueMemory.Length));
            propertyNameMemory
                .CopyTo(result.Span.SliceAndIncrement(ref index, propertyNameMemory.Length));

            return result;
        }

        public static StringSyncPacket Deserialize(ReadOnlyMemory<byte> memory)
        {
            var index = 0;
            var objectSyncId = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var newValueLength = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var newValue = Encoding.UTF8.GetString(memory.Span.SliceAndIncrement(ref index, newValueLength));
            var propertyName = Encoding.UTF8.GetString(memory.Span[index..]);

            return new StringSyncPacket(objectSyncId, propertyName, SyncOptions.None, newValue);
        }
    }
}