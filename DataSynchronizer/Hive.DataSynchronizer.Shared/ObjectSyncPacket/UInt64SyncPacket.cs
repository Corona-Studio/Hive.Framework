using System.Text;
using System;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.DataSynchronizer.Shared.ObjectSyncPacket
{
    public class UInt64SyncPacket : AbstractObjectSyncPacket<ulong>
    {
        public UInt64SyncPacket(
            ushort objectSyncId,
            string propertyName,
            SyncOptions syncOptions,
            ulong newValue) : base(objectSyncId, propertyName, syncOptions, newValue)
        {
        }

        public override ReadOnlyMemory<byte> Serialize()
        {
            var propertyNameMemory = Encoding.UTF8.GetBytes(PropertyName).AsSpan();

            var totalLength = sizeof(ushort) + sizeof(ulong) + propertyNameMemory.Length;
            var result = new Memory<byte>(new byte[totalLength]);

            var index = 0;

            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(ushort)),
                ObjectSyncId);
            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(ulong)),
                NewValue);
            propertyNameMemory
                .CopyTo(result.Span.SliceAndIncrement(ref index, propertyNameMemory.Length));

            return result;
        }

        public static UInt64SyncPacket Deserialize(ReadOnlyMemory<byte> memory)
        {
            var index = 0;
            var objectSyncId = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var newValue = BitConverter.ToUInt64(memory.Span.SliceAndIncrement(ref index, sizeof(ulong)));
            var propertyName = Encoding.UTF8.GetString(memory.Span[index..]);

            return new UInt64SyncPacket(objectSyncId, propertyName, SyncOptions.None, newValue);
        }
    }
}