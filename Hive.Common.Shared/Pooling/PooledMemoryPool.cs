using System;
using System.Buffers;

namespace Hive.Common.Shared.Pooling
{
    /// <summary>Array-backed pipe segments with reusable owner wrappers.</summary>
    public sealed class PooledMemoryPool : MemoryPool<byte>
    {
        public static new PooledMemoryPool Shared { get; } = new();
        private PooledMemoryPool() { }
        public override int MaxBufferSize => int.MaxValue;

        public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
        {
            if (minBufferSize < -1) throw new ArgumentOutOfRangeException(nameof(minBufferSize));
            var size = Math.Max(1, minBufferSize == -1 ? 4096 : minBufferSize);
            var owner = PooledBufferStream.Rent(size);
            try
            {
                owner.GetMemory(size);
                owner.Advance(size);
                return owner;
            }
            catch
            {
                owner.Dispose();
                throw;
            }
        }
        protected override void Dispose(bool disposing) { }
    }
}
