﻿using System;
using System.Text;
using Hive.Common.Shared;
using Hive.Common.Shared.Helpers;

namespace Hive.DataSync.Shared.ObjectSyncPacket
{
    public class BooleanSyncPacket : AbstractObjectSyncPacket<bool>
    {
        public BooleanSyncPacket(
            ushort objectSyncId,
            string propertyName,
            SyncOptions syncOptions,
            bool newValue) : base(objectSyncId, propertyName, syncOptions, newValue)
        {
        }

        public override ReadOnlyMemory<byte> Serialize()
        {
            var propertyNameMemory = Encoding.UTF8.GetBytes(PropertyName).AsSpan();

            // [OBJ_SYNC_ID (2) | NEW_VAL | PROPERTY_NAME]
            var totalLength = sizeof(ushort) + sizeof(bool) + propertyNameMemory.Length;
            var result = new Memory<byte>(new byte[totalLength]);

            var index = 0;

            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(ushort)),
                ObjectSyncId);
            BitConverter.TryWriteBytes(
                result.Span.SliceAndIncrement(ref index, sizeof(bool)),
                NewValue);
            propertyNameMemory
                .CopyTo(result.Span.SliceAndIncrement(ref index, propertyNameMemory.Length));

            return result;
        }

        public static BooleanSyncPacket Deserialize(ReadOnlyMemory<byte> memory)
        {
            var index = 0;
            var objectSyncId = BitConverter.ToUInt16(memory.Span.SliceAndIncrement(ref index, sizeof(ushort)));
            var newValue = BitConverter.ToBoolean(memory.Span.SliceAndIncrement(ref index, sizeof(bool)));
            var propertyName = Encoding.UTF8.GetString(memory.Span[index..]);

            return new BooleanSyncPacket(objectSyncId, propertyName, SyncOptions.None, newValue);
        }
    }
}