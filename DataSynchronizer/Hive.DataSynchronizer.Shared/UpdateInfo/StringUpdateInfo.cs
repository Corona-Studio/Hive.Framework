using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using System.Buffers;
using System.Text;
using System;
using Hive.Framework.Shared.Helpers;

namespace Hive.DataSynchronizer.Shared.UpdateInfo
{
    public class StringUpdateInfo : AbstractUpdateInfoBase<string>
    {
        public StringUpdateInfo(ushort objectSyncId, string propertyName, string newValue) : base(objectSyncId, propertyName, newValue)
        {
        }

        public override ReadOnlyMemory<byte> Serialize<TId>(IPacketCodec<TId> codec)
        {
            // [LENGTH (2) | PACKET_FLAGS (4) | TYPE (2) | CONTENT [STR LEN | STR | PROPERTY NAME] ]
            var packetIdMemory = codec.PacketIdMapper.GetPacketIdMemory(typeof(Int64UpdateInfo));
            const PacketFlags packetFlags = PacketFlags.Broadcast | PacketFlags.S2CPacket;
            var newValueMemory = Encoding.UTF8.GetBytes(NewValue).AsSpan();
            var propertyNameMemory = Encoding.UTF8.GetBytes(PropertyName).AsSpan();

            var totalLength = sizeof(ushort) + sizeof(uint) + packetIdMemory.Length + sizeof(ushort) + newValueMemory.Length + propertyNameMemory.Length;
            var result = new Memory<byte>(new byte[totalLength]);

            var index = 0;

            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(ushort)),
                (ushort)totalLength);
            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(uint)),
                (uint)packetFlags);
            packetIdMemory.Span
                .CopyTo(result.Span.SliceAndIncrement(ref index, packetIdMemory.Length));
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

        public override AbstractUpdateInfoBase Deserialize(ReadOnlyMemory<byte> memory)
        {
            var index = 0;
            var objectSyncId = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var newValueLength = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var newValue = Encoding.UTF8.GetString(memory.Span.SliceAndIncrement(ref index, newValueLength));
            var propertyName = Encoding.UTF8.GetString(memory.Span[index..]);

            return new StringUpdateInfo(objectSyncId, propertyName, newValue);
        }
    }
}