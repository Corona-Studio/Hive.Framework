using DotNext.Buffers;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using ProtoBuf;
using System;
using System.Buffers;

namespace Hive.Framework.Codec.Protobuf;

public class ProtoBufPackerCodec : IPacketCodec<ushort>
{
    private static readonly ObjectPool<PooledBufferWriter<byte>> WriterPool;

    static ProtoBufPackerCodec()
    {
        WriterPool = new ObjectPool<PooledBufferWriter<byte>>(
            () => new PooledBufferWriter<byte>
            {
                BufferAllocator = UnmanagedMemoryPool<byte>.Shared.ToAllocator()
            },
            writer => writer.Clear());
    }

    public IPacketIdMapper<ushort> PacketIdMapper { get; init; } = null!;

    public ReadOnlyMemory<byte> Encode<T>(T obj)
    {
        var writer = WriterPool.Get();

        using var contentMeasure = Serializer.Measure(obj);

        if (contentMeasure.Length > ushort.MaxValue)
            throw new InvalidOperationException($"Message to large [Length - {contentMeasure.Length}]");

        var packetId = PacketIdMapper.GetPacketId(typeof(T));

        Span<byte> lengthHeader = stackalloc byte[2];
        Span<byte> typeHeader = stackalloc byte[2];

        // Packet Length
        BitConverter.TryWriteBytes(lengthHeader, (ushort)contentMeasure.Length + 2);
        writer.Write(lengthHeader);

        // Packet Id
        BitConverter.TryWriteBytes(typeHeader, packetId);
        writer.Write(typeHeader);

        contentMeasure.Serialize(writer);

        var result = writer.WrittenMemory;

        WriterPool.Return(writer);

        return result;
    }

    public object Decode(ReadOnlySpan<byte> data)
    {
        // 负载长度
        // var packetLengthSpan = data[..2];

        // 封包类型
        var packetIdSpan = data.Slice(2, 2);
        var packetId = BitConverter.ToUInt16(packetIdSpan);

        // 封包数据段
        var packetData = data[2..];

        // var packetLength = BitConverter.ToUInt16(packetLengthSpan);
        var packetType = PacketIdMapper.GetPacketType(packetId);

        return Serializer.Deserialize(packetData, packetType);
    }
}