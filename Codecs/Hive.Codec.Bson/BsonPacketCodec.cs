﻿using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using Hive.Framework.Shared.Helpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Hive.Framework.Codec.Bson;

public class BsonPacketCodec : IPacketCodec<ushort>
{
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
        var flagsMemory = data.Slice(2, 4);
        var flags = BitConverter.ToUInt32(flagsMemory.Span);

        return (PacketFlags) flags;
    }

    public ReadOnlyMemory<byte> Encode<T>(T obj, PacketFlags flags)
    {
        var dataSpan = obj.ToBson().AsSpan();

        if (dataSpan.Length + 8 > ushort.MaxValue)
            throw new InvalidOperationException($"Message too large [Length - {dataSpan.Length}]");

        var packetId = PacketIdMapper.GetPacketId(typeof(T));

        // [LENGTH (2) | PACKET_FLAGS (4) | TYPE (2) | CONTENT]
        var index = 0;
        var result = new Memory<byte>(new byte[2 + 4 + 2 + dataSpan.Length]);
        
        BitConverter.TryWriteBytes(
            result.Span.SliceAndIncrement(ref index, sizeof(ushort)),
            (ushort)(dataSpan.Length + 4 + 2));

        // Packet Flags
        BitConverter.TryWriteBytes(
            result.Span.SliceAndIncrement(ref index, sizeof(uint)),
            (uint)flags);

        // Packet Id
        BitConverter.TryWriteBytes(
            result.Span.SliceAndIncrement(ref index, sizeof(ushort)),
            packetId);

        // Packet Payload
        dataSpan.CopyTo(result.Span.SliceAndIncrement(ref index, dataSpan.Length));

        return result;
    }

    public unsafe PacketDecodeResultWithId<ushort> Decode(ReadOnlySpan<byte> data)
    {
        // 负载长度
        // var packetLengthSpan = data[..2];
        // [LENGTH (2) | PACKET_FLAGS (4) | TYPE (2) | CONTENT]

        // 封包标志
        var packetFlagsSpan = data.Slice(2, 4);
        var flagsUint = BitConverter.ToUInt32(packetFlagsSpan);
        var flags = (PacketFlags)flagsUint;

        if (!flags.HasFlag(PacketFlags.HasCustomPacketPrefix) &&
            flags.HasFlag(PacketFlags.NoPayload))
            return new PacketDecodeResultWithId<ushort>(flags, 0);

        // 封包前缀
        var hasPayload = !flags.HasFlag(PacketFlags.NoPayload);
        var payloadStartIndex = hasPayload ? 8 : 6;
        var packetPrefixes = Array.Empty<object?>();

        if (flags.HasFlag(PacketFlags.HasCustomPacketPrefix) && (PrefixResolvers?.Any() ?? false))
        {
            packetPrefixes = new object[PrefixResolvers.Length];
            for (var i = 0; i < PrefixResolvers.Length; i++)
            {
                packetPrefixes[i] = PrefixResolvers[i].Resolve(data, ref payloadStartIndex);
            }
        }

        if (!hasPayload)
            return new PacketDecodeResultWithId<ushort>(packetPrefixes, flags, 0);

        // 封包类型
        // 因为封包类型字段可能不存在，因此我们要在读取封包前缀后再解析封包 ID
        var packetIdSpan = data.Slice(6, 2);
        var packetId = BitConverter.ToUInt16(packetIdSpan);

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