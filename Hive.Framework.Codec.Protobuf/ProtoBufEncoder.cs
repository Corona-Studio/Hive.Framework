using System;
using DotNext.Buffers;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;    

namespace Hive.Framework.Codec.Protobuf;

public class ProtoBufEncoder : IEncoder<byte>
{
    private static readonly ObjectPool<PooledBufferWriter<byte>> WriterPool;

    static ProtoBufEncoder()
    {
        WriterPool = new ObjectPool<PooledBufferWriter<byte>>(
            () => new PooledBufferWriter<byte> 
            { 
                BufferAllocator = UnmanagedMemoryPool<byte>.Shared.ToAllocator()
            },
            writer => writer.Clear());
    }

    public IPacketGenerator<byte> PacketGenerator { get; init; } = null!;

    public ReadOnlySpan<byte> Encode<T>(T obj) where T : unmanaged
    {
        var writer = WriterPool.Get();
        
        PacketGenerator.Generate(obj, writer);
        
        WriterPool.Return(writer);

        return writer.WrittenMemory.Span;
    }
}