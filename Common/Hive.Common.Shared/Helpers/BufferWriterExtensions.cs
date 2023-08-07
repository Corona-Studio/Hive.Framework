using System;
using System.Buffers;

namespace Hive.Framework.Shared.Helpers
{
    public static class BufferWriterExtensions
    {
        public static void WriteGuid(this IBufferWriter<byte> writer, Guid val)
        {
            writer.Write(val.ToByteArray());
        }
    }
}