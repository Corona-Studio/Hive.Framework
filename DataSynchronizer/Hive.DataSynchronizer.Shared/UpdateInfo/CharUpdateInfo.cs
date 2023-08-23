using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using System.Buffers;
using System.Text;
using System;
using Hive.Framework.Shared.Helpers;

namespace Hive.DataSynchronizer.Shared.UpdateInfo
{
    public class CharUpdateInfo : AbstractUpdateInfoBase<char>
    {
        public CharUpdateInfo(ushort objectSyncId, string propertyName, char newValue) : base(objectSyncId, propertyName, newValue)
        {
        }

        public override ReadOnlyMemory<byte> Serialize<TId>(IPacketCodec<TId> codec)
        {
            // [LENGTH (2) | PACKET_FLAGS (4) | TYPE (2) | CONTENT]
            var packetIdMemory = codec.PacketIdMapper.GetPacketIdMemory(typeof(CharUpdateInfo));
            const PacketFlags packetFlags = PacketFlags.Broadcast | PacketFlags.S2CPacket;
            var propertyNameMemory = Encoding.UTF8.GetBytes(PropertyName).AsSpan();

            var totalLength = sizeof(ushort) + sizeof(uint) + packetIdMemory.Length + sizeof(ushort) + sizeof(char) + propertyNameMemory.Length;
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
                result.Span.SliceAndIncrement(ref index, sizeof(char)),
                NewValue);
            propertyNameMemory
                .CopyTo(result.Span.SliceAndIncrement(ref index, propertyNameMemory.Length));

            return result;
        }

        public override AbstractUpdateInfoBase Deserialize(ReadOnlyMemory<byte> memory)
        {
            var index = 0;
            var objectSyncId = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var newValue = BitConverter.ToChar(memory.Span.SliceAndIncrement(ref index, sizeof(char)));
            var propertyName = Encoding.UTF8.GetString(memory.Span[index..]);

            return new CharUpdateInfo(objectSyncId, propertyName, newValue);
        }
    }
}