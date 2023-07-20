using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
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

    public BsonPacketCodec(IPacketIdMapper<ushort> packetIdMapper, IPacketPrefixResolver[]? prefixResolvers = null)
    {
        PacketIdMapper = packetIdMapper;
        PrefixResolvers = prefixResolvers;
    }

    public IPacketIdMapper<ushort> PacketIdMapper { get; }
    public IPacketPrefixResolver[]? PrefixResolvers { get; }

    public ReadOnlyMemory<byte> GetPacketIdMemory(ReadOnlyMemory<byte> payload)
    {
        if (payload.IsEmpty)
            throw new InvalidOperationException($"{nameof(payload)} has length of 0!");

        return payload.Slice(2, 2);
    }

    public ushort GetPacketId(ReadOnlyMemory<byte> idMemory)
    {
        return BitConverter.ToUInt16(idMemory.Span);
    }

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

    public unsafe PacketDecodeResult<ushort> Decode(ReadOnlySpan<byte> data)
    {
        // 负载长度
        // var packetLengthSpan = data[..2];

        // 封包类型
        var packetIdSpan = data.Slice(2, 2);
        var packetId = BitConverter.ToUInt16(packetIdSpan);

        // 封包前缀
        var payloadStartIndex = 4;
        var packetPrefixes = Array.Empty<object?>();
        if (PrefixResolvers?.Any() ?? false)
        {
            packetPrefixes = new object[PrefixResolvers.Length];
            for (var i = 0; i < PrefixResolvers.Length; i++)
            {
                packetPrefixes[i] = PrefixResolvers[i].Resolve(data, ref payloadStartIndex);
            }
        }

        // 封包数据段
        var packetData = data[payloadStartIndex..];

        fixed (byte* bp = &packetData.GetPinnableReference())
        {
            using var dataMs = new UnmanagedMemoryStream(bp, packetData.Length);

            // var packetLength = BitConverter.ToUInt16(packetLengthSpan);
            var packetType = PacketIdMapper.GetPacketType(packetId);
            var payload = BsonSerializer.Deserialize(dataMs, packetType);

            return new PacketDecodeResult<ushort>(packetPrefixes, packetId, payload);
        }
    }
}