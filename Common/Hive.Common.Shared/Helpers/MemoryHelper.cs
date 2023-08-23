using System;
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

        public static ReadOnlySpan<T> SliceAndIncrement<T>(this ReadOnlySpan<T> span, ref int index, int length)
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

        public static Memory<T> Copy<T>(this Memory<T> old)
        {
            var result = new Memory<T>(new T[old.Length]);

            old.CopyTo(result);

            return result;
        }

        public static ReadOnlyMemory<T> Copy<T>(this ReadOnlyMemory<T> old)
        {
            var result = new Memory<T>(new T[old.Length]);

            old.CopyTo(result);

            return result;
        }

        public static ReadOnlyMemory<byte> CombineMemory(params ReadOnlyMemory<byte>[] memories)
        {
            var totalSize = memories.Sum(m => m.Length);
            var result = new Memory<byte>(new byte[totalSize]);

            var index = 0;

            foreach (var memory in memories)
            {
                memory.Span.CopyTo(result.Span.SliceAndIncrement(ref index, memory.Length));
            }

            return result;
        }
    }
}