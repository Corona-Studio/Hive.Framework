using System;
using System.Buffers;

namespace Hive.Framework.Shared
{
    public readonly struct SerializedPacketMemory : IDisposable
    {
        public int Length { get; }
        public IMemoryOwner<byte> MemoryOwner { get; }

        public SerializedPacketMemory(int length, IMemoryOwner<byte> memoryOwner)
        {
            Length = length;
            MemoryOwner = memoryOwner;
        }

        public void Dispose()
        {
            MemoryOwner.Dispose();
        }
    }
}