using System;
using System.Buffers;
using System.Linq;

namespace Hive.Framework.Shared
{
    public static class MemoryHelper
    {
        public static ReadOnlyMemory<T> CombineMemory<T>(int size, params ReadOnlyMemory<T>[] memories)
        {
            var index = 0;
            var resultSize = size == 0 ? memories.Sum(m => m.Length) : size;

            using var rent = MemoryPool<T>.Shared.Rent(resultSize);
            var resultMem = rent.Memory;

            foreach (var memory in memories)
            {
                memory.CopyTo(resultMem[index..]);
                index += memory.Length;
            }

            return resultMem;
        }
    }
}