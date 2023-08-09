using System;
using System.Buffers;

namespace Hive.Framework.Codec.Protobuf;

public class FakeMemoryBufferWriter<T> : IBufferWriter<T>
{
    private int _index;
    private readonly Memory<T> _memory;

    public FakeMemoryBufferWriter(Memory<T> memory)
    {
        _memory = memory;
        _index = 0;
    }

    public void Advance(int count)
    {
        if (count < 0)
            throw new ArgumentException(null, nameof(count));
        if (_index > _memory.Length - count)
            throw new InvalidOperationException("Cannot advance past the end of the buffer.");

        _index += count;
    }

    public Memory<T> GetMemory(int sizeHint = 0) => _memory[_index..];

    public Span<T> GetSpan(int sizeHint = 0) => _memory.Span[_index..];
}