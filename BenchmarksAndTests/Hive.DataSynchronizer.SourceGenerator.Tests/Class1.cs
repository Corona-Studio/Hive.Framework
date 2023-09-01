using System;
using System.Text;
using Hive.DataSynchronizer.Shared.Attributes;
using Hive.DataSynchronizer.Shared.UpdateInfo;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.DataSynchronizer.SourceGenerator.Tests
{
    public class GuidUpdateInfo : AbstractUpdateInfoBase<Guid>
    {
        public GuidUpdateInfo(ushort objectSyncId, string propertyName, Guid newValue) : base(objectSyncId, propertyName, newValue)
        {
        }

        public override ReadOnlyMemory<byte> Serialize<TId>(IPacketCodec<TId> codec)
        {
            // [LENGTH (2) | PACKET_FLAGS (4) | TYPE (2) | CONTENT]
            var packetIdMemory = codec.PacketIdMapper.GetPacketIdMemory(typeof(UInt32UpdateInfo));
            var packetFlags = PacketFlags.Broadcast | PacketFlags.S2CPacket;
            var propertyNameMemory = Encoding.UTF8.GetBytes(PropertyName).AsSpan();

            var totalLength = sizeof(ushort) + sizeof(uint) + packetIdMemory.Length + sizeof(ushort) + sizeof(uint) + propertyNameMemory.Length;
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
            NewValue.ToByteArray().AsSpan()
                .CopyTo(result.Span.SliceAndIncrement(ref index, 16));
            propertyNameMemory
                .CopyTo(result.Span.SliceAndIncrement(ref index, propertyNameMemory.Length));

            return result;
        }

        public override AbstractUpdateInfoBase Deserialize(ReadOnlyMemory<byte> memory)
        {
            var index = 0;
            var objectSyncId = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var newValue = new Guid(memory.Span.SliceAndIncrement(ref index, 16));
            var propertyName = Encoding.UTF8.GetString(memory.Span[index..]);

            return new GuidUpdateInfo(objectSyncId, propertyName, newValue);
        }
    }

    [SyncObject(1)]
    public partial class Class1
    {
        [SyncProperty]
        private int _in;

        [SyncProperty]
        [CustomSerializer(typeof(GuidUpdateInfo))]
        private Guid _guidTest;
    }
}
