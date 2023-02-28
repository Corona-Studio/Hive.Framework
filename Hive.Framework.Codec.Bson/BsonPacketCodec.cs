using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using System;
using System.Buffers;
using System.IO;
using DotNext.Buffers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Hive.Framework.Codec.Bson;

public class BsonPacketCodec : IPacketCodec<ushort>
{
    private static readonly ObjectPool<PooledBufferWriter<byte>> WriterPool;

    static BsonPacketCodec()
    {
        WriterPool = new ObjectPool<PooledBufferWriter<byte>>(
            () => new PooledBufferWriter<byte>
            {
                BufferAllocator = UnmanagedMemoryPool<byte>.Shared.ToAllocator()
            },
            writer => writer.Clear());
    }

    public BsonPacketCodec(IPacketIdMapper<ushort> packetIdMapper)
    {
        PacketIdMapper = packetIdMapper;
    }

    public IPacketIdMapper<ushort> PacketIdMapper { get; }

    public ReadOnlyMemory<byte> Encode<T>(T obj)
    {
        var writer = WriterPool.Get();
        var dataSpan = obj.ToBson().AsSpan();

        if (dataSpan.Length + 4 > ushort.MaxValue)
            throw new InvalidOperationException($"Message to large [Length - {dataSpan.Length}]");

        var packetId = PacketIdMapper.GetPacketId(typeof(T));

        Span<byte> lengthHeader = stackalloc byte[2];
        Span<byte> typeHeader = stackalloc byte[2];

        // Packet Length [LENGTH (2) | TYPE (2) | CONTENT]
        BitConverter.TryWriteBytes(lengthHeader, (ushort)(dataSpan.Length + 2));
        writer.Write(lengthHeader);

        // Packet Id
        BitConverter.TryWriteBytes(typeHeader, packetId);
        writer.Write(typeHeader);

        writer.Write(dataSpan);

        var result = writer.WrittenMemory;

        WriterPool.Return(writer);

        return result;
    }

    public unsafe object Decode(ReadOnlySpan<byte> data)
    {
        // 负载长度
        // var packetLengthSpan = data[..2];

        // 封包类型
        var packetIdSpan = data.Slice(2, 2);
        var packetId = BitConverter.ToUInt16(packetIdSpan);

        // 封包数据段
        var packetData = data[4..];

        fixed (byte* bp = &packetData.GetPinnableReference())
        {
            using var dataMs = new UnmanagedMemoryStream(bp, data.Length);

            // var packetLength = BitConverter.ToUInt16(packetLengthSpan);
            var packetType = PacketIdMapper.GetPacketType(packetId);


            return BsonSerializer.Deserialize(dataMs, packetType);
        }
    }
}