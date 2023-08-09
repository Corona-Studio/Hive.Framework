using System;
using System.Buffers;
using System.Linq;

namespace Hive.Framework.Shared.Helpers
{
    public static class MemoryHelper
    {
        public static Span<T> SliceAndIncrement<T>(this Span<T> span, ref int index, int length)
        {
            var result = span.Slice(index, length);

            index += length;

            return result;
        }

        public static Memory<T> SliceAndIncrement<T>(this Memory<T> span, ref int index, int length)
        {
            var result = span.Slice(index, length);

            index += length;

            return result;
        }

        public static SerializedPacketMemory CombineMemory(params ReadOnlyMemory<byte>[] memories)
        {
            var totalSize = memories.Sum(m => m.Length);
            var rentMemory = MemoryPool<byte>.Shared.Rent(totalSize);

            var index = 0;

            foreach (var memory in memories)
            {
                memory.Span.CopyTo(rentMemory.Memory.Span.SliceAndIncrement(ref index, memory.Length));
            }

            return new SerializedPacketMemory(totalSize, rentMemory);
        }
    }
}