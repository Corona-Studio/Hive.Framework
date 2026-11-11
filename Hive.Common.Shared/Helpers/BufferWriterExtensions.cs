using System;
using System.Buffers;

namespace Hive.Common.Shared.Helpers
{
    public static class BufferWriterExtensions
    {
        extension(IBufferWriter<byte> writer)
        {
            public void WriteGuid(Guid val)
            {
                writer.Write(val.ToByteArray());
            }
        }
    }
}