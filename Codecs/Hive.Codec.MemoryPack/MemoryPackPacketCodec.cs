using Hive.Framework.Codec.Abstractions;
using MemoryPack;
using System.Buffers;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.Common.Codec.MemoryPack;

public class MemoryPackPacketCodec : IPacketCodec<ushort>
{
    public MemoryPackPacketCodec(
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

        return (PacketFlags)flags;
    }

    public SerializedPacketMemory Encode<T>(T obj, PacketFlags flags)
    {
        var dataSpan = MemoryPackSerializer.Serialize(obj).AsSpan();

        if (dataSpan.Length + 8 > ushort.MaxValue)
            throw new InvalidOperationException($"Message too large [Length - {dataSpan.Length}]");

        var packetId = PacketIdMapper.GetPacketId(typeof(T));

        // [LENGTH (2) | PACKET_FLAGS (4) | TYPE (2) | CONTENT]
        var index = 0;
        var result = MemoryPool<byte>.Shared.Rent(2 + 4 + 2 + dataSpan.Length);

        BitConverter.TryWriteBytes(
            result.Memory.Span.SliceAndIncrement(ref index, sizeof(ushort)),
            (ushort)(dataSpan.Length + 4 + 2));

        // Packet Flags
        BitConverter.TryWriteBytes(
            result.Memory.Span.SliceAndIncrement(ref index, sizeof(uint)),
            (uint)flags);

        // Packet Id
        BitConverter.TryWriteBytes(
            result.Memory.Span.SliceAndIncrement(ref index, sizeof(ushort)),
            packetId);

        // Packet Payload
        dataSpan.CopyTo(result.Memory.Span.SliceAndIncrement(ref index, dataSpan.Length));

        return new SerializedPacketMemory(index, result);
    }

    public PacketDecodeResultWithId<ushort> Decode(ReadOnlySpan<byte> data)
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

        if (flags.HasFlag(PacketFlags.HasCustomPacketPrefix) && (PrefixResolvers?.Any() ?? false))
        {
            packetPrefixes = new object[PrefixResolvers.Length];
            for (var i = 0; i < PrefixResolvers.Length; i++)
            {
                packetPrefixes[i] = PrefixResolvers[i].Resolve(data, ref payloadStartIndex);
            }
        }

        // 封包数据段
        var packetData = data[payloadStartIndex..];

        // var packetLength = BitConverter.ToUInt16(packetLengthSpan);
        var packetType = PacketIdMapper.GetPacketType(packetId);
        var payload = MemoryPackSerializer.Deserialize(packetType, packetData);

        return new PacketDecodeResultWithId<ushort>(packetPrefixes, flags, packetId, payload);
    }
}