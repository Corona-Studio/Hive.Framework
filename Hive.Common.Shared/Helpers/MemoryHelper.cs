using System;
using System.Linq;

namespace Hive.Common.Shared.Helpers
{
    public static class MemoryHelper
    {
        extension<T>(Span<T> span)
        {
            public Span<T> SliceAndIncrement(ref int index, int length)
            {
                var result = span.Slice(index, length);

                index += length;

                return result;
            }
        }

        extension<T>(ReadOnlySpan<T> span)
        {
            public ReadOnlySpan<T> SliceAndIncrement(ref int index, int length)
            {
                var result = span.Slice(index, length);

                index += length;

                return result;
            }
        }

        extension<T>(Memory<T> memory)
        {
            public Memory<T> SliceAndIncrement(ref int index, int length)
            {
                var result = memory.Slice(index, length);

                index += length;

                return result;
            }

            public Memory<T> Copy()
            {
                var result = new Memory<T>(new T[memory.Length]);

                memory.CopyTo(result);

                return result;
            }
        }

        extension<T>(ReadOnlyMemory<T> old)
        {
            public ReadOnlyMemory<T> Copy()
            {
                var result = new Memory<T>(new T[old.Length]);

                old.CopyTo(result);

                return result;
            }
        }

        public static ReadOnlyMemory<byte> CombineMemory(params ReadOnlyMemory<byte>[] memories)
        {
            var totalSize = memories.Sum(m => m.Length);
            var result = new Memory<byte>(new byte[totalSize]);

            var index = 0;

            foreach (var memory in memories)
            {
                if (memory.IsEmpty) continue;
                memory.Span.CopyTo(result.Span.SliceAndIncrement(ref index, memory.Length));
            }

            return result;
        }
    }
}