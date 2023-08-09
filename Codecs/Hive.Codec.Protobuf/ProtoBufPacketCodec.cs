using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using ProtoBuf;
using System;
using System.Buffers;
using ProtoBuf.Meta;
using System.Linq;
using Hive.Framework.Shared.Helpers;

namespace Hive.Framework.Codec.Protobuf;

public class ProtoBufPacketCodec : IPacketCodec<ushort>
{
    public ProtoBufPacketCodec(
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
        using var contentMeasure = Serializer.Measure(obj);

        if (contentMeasure.Length + 8 > ushort.MaxValue || contentMeasure.Length > int.MaxValue)
            throw new InvalidOperationException($"Message too large [Length - {contentMeasure.Length}]");

        var packetId = PacketIdMapper.GetPacketId(typeof(T));

        // [LENGTH (2) | PACKET_FLAGS (4) | TYPE (2) | CONTENT]
        var index = 0;
        var result = MemoryPool<byte>.Shared.Rent(2 + 4 + 2 + (int)contentMeasure.Length);

        BitConverter.TryWriteBytes(
            result.Memory.Span.SliceAndIncrement(ref index, sizeof(ushort)),
            (ushort)(contentMeasure.Length + 4 + 2));

        // Packet Flags
        BitConverter.TryWriteBytes(
            result.Memory.Span.SliceAndIncrement(ref index, sizeof(uint)),
            (uint)flags);

        // Packet Id
        BitConverter.TryWriteBytes(
            result.Memory.Span.SliceAndIncrement(ref index, sizeof(ushort)),
            packetId);

        // Packet Payload
        var payloadMemory = result.Memory.SliceAndIncrement(ref index, (int) contentMeasure.Length);
        var writer = new FakeMemoryBufferWriter<byte>(payloadMemory);

        contentMeasure.Serialize(writer);

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
        var payload = RuntimeTypeModel.Default.Deserialize(packetType, packetData);

        return new PacketDecodeResultWithId<ushort>(packetPrefixes, flags, packetId, payload);
    }
}