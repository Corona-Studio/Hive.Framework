using System;
using System.Text;
using Hive.DataSync.Shared.Attributes;
using Hive.DataSync.Shared.ObjectSyncPacket;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.DataSync.SourceGenerator.Tests
{
    public class GuidSyncPacket : AbstractObjectSyncPacket<Guid>
    {
        public GuidSyncPacket(
            ushort objectSyncId,
            string propertyName,
            SyncOptions syncOptions,
            Guid newValue) : base(objectSyncId, propertyName, syncOptions, newValue)
        {
        }

        public override ReadOnlyMemory<byte> Serialize()
        {
            var propertyNameMemory = Encoding.UTF8.GetBytes(PropertyName).AsSpan();

            var totalLength = sizeof(ushort) + 16 + propertyNameMemory.Length;
            var result = new Memory<byte>(new byte[totalLength]);

            var index = 0;

            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(ushort)),
                ObjectSyncId);
            NewValue.ToByteArray().AsSpan()
                .CopyTo(result.Span.SliceAndIncrement(ref index, 16));
            propertyNameMemory
                .CopyTo(result.Span.SliceAndIncrement(ref index, propertyNameMemory.Length));

            return result;
        }

        public static AbstractObjectSyncPacket Deserialize(ReadOnlyMemory<byte> memory)
        {
            var index = 0;
            var objectSyncId = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var newValue = new Guid(memory.Span.SliceAndIncrement(ref index, 16));
            var propertyName = Encoding.UTF8.GetString(memory.Span[index..]);

            return new GuidSyncPacket(objectSyncId, propertyName, SyncOptions.None, newValue);
        }
    }

    [SyncObject(1)]
    [SetSyncInterval(100)]
    public partial class Class1
    {
        [SyncProperty]
        private double _test;

        [SyncProperty]
        [SyncOption(SyncOptions.ServerOnly)]
        private int _in;

        [SyncProperty]
        [SyncOption(SyncOptions.AllSession)]
        [CustomSerializer(typeof(GuidSyncPacket))]
        private Guid _guidTest;
    }
}
