using System;
using System.Buffers;

namespace Hive.Framework.Shared
{
    public static class MemoryHelper
    {
        public static ReadOnlyMemory<T> CombineMemory<T>(params ReadOnlyMemory<T>[] memories)
        {
            var writer = new ArrayBufferWriter<T>(50);

            foreach (var memory in memories)
                writer.Write(memory.Span);

            return writer.WrittenMemory;
        }
    }
}