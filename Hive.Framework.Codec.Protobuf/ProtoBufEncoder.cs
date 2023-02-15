using System;
using System.Buffers;
using DotNext.Buffers;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using ProtoBuf;

namespace Hive.Framework.Codec.Protobuf;

public class ProtoBufEncoder : IEncoder<ushort>
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

    public IPacketIdMapper<ushort> PacketIdMapper { get; init; } = null!;

    public ReadOnlySpan<byte> Encode<T>(T obj) where T : unmanaged
    {
        var writer = WriterPool.Get();

        using var contentMeasure = Serializer.Measure(obj);

        if (contentMeasure.Length > ushort.MaxValue)
            throw new InvalidOperationException($"Message to large [Length - {contentMeasure.Length}]");

        var packetId = PacketIdMapper.GetPacketId(typeof(T));

        Span<byte> lengthHeader = stackalloc byte[2];
        Span<byte> typeHeader = stackalloc byte[2];

        // Packet Length
        BitConverter.TryWriteBytes(lengthHeader, (ushort)contentMeasure.Length);
        writer.Write(lengthHeader);

        // Packet Id
        BitConverter.TryWriteBytes(typeHeader, packetId);
        writer.Write(typeHeader);

        contentMeasure.Serialize(writer);

        var result = writer.WrittenMemory.Span;

        WriterPool.Return(writer);

        return result;
    }
}