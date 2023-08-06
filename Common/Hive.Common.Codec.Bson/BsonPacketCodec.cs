using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ObjectPool;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Hive.Framework.Codec.Bson;

public class BsonPacketCodec : IPacketCodec<ushort>
{
    private static readonly ObjectPool<ArrayBufferWriter<byte>> WriterPool;

    static BsonPacketCodec()
    {
        WriterPool = new DefaultObjectPool<ArrayBufferWriter<byte>>(new BufferWriterPoolPolicy(), 20);
    }

    public BsonPacketCodec(
        IPacketIdMapper<ushort> packetIdMapper,
        IPacketPrefixResolver[]? prefixResolvers = null)
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

        return payload.Slice(6, 2);
    }

    public ushort GetPacketId(ReadOnlyMemory<byte> idMemory)
    {
        return BitConverter.ToUInt16(idMemory.Span);
    }

    public ReadOnlyMemory<byte> GetPacketFlagsMemory(ReadOnlyMemory<byte> payload)
    {
        if (payload.IsEmpty)
            throw new InvalidOperationException($"{nameof(payload)} has length of 0!");

        return payload.Slice(2, 4);
    }

    public PacketFlags GetPacketFlags(ReadOnlyMemory<byte> data)
    {
        var flagsMemory = data.Slice(6, 4);
        var flags = BitConverter.ToUInt32(flagsMemory.Span);

        return (PacketFlags) flags;
    }

    public ReadOnlyMemory<byte> Encode<T>(T obj, PacketFlags flags)
    {
        var writer = WriterPool.Get();

        try
        {
            var dataSpan = obj.ToBson().AsSpan();

            if (dataSpan.Length + 4 > ushort.MaxValue)
                throw new InvalidOperationException($"Message to large [Length - {dataSpan.Length}]");

            var packetId = PacketIdMapper.GetPacketId(typeof(T));

            Span<byte> lengthHeader = stackalloc byte[2];
            Span<byte> flagsHeader = stackalloc byte[4];
            Span<byte> typeHeader = stackalloc byte[2];

            // [LENGTH (2) | PACKET_FLAGS (4) | TYPE (2) | CONTENT]
            BitConverter.TryWriteBytes(lengthHeader, (ushort)(dataSpan.Length + 4 + 2));
            writer.Write(lengthHeader);

            // Packet Flags
            BitConverter.TryWriteBytes(flagsHeader, (uint)flags);
            writer.Write(flagsHeader);

            // Packet Id
            BitConverter.TryWriteBytes(typeHeader, packetId);
            writer.Write(typeHeader);

            writer.Write(dataSpan);
            
            return writer.WrittenMemory.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
        finally
        {
            WriterPool.Return(writer);
        }
    }

    public unsafe PacketDecodeResultWithId<ushort> Decode(ReadOnlySpan<byte> data)
    {
        // 负载长度
        // var packetLengthSpan = data[..2];

        // 封包标志
        var packetFlagsSpan = data.Slice(2, 4);
        var flagsUint = BitConverter.ToUInt32(packetFlagsSpan);
        var flags = (PacketFlags)flagsUint;

        // 封包类型
        var packetIdSpan = data.Slice(6, 2);
        var packetId = BitConverter.ToUInt16(packetIdSpan);

        // 封包前缀
        var payloadStartIndex = 8;
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
            
            return new PacketDecodeResultWithId<ushort>(packetPrefixes, flags, packetId, payload);
        }
    }
}