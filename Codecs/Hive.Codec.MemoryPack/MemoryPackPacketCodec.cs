using Hive.Framework.Codec.Abstractions;
using MemoryPack;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Helpers;
using System.Collections.Concurrent;

namespace Hive.Codec.MemoryPack;

public class MemoryPackPacketCodec : IPacketCodec<ushort>
{
    private readonly ConcurrentDictionary<Type, (Func<object, ReadOnlyMemory<byte>>, Func<ReadOnlyMemory<byte>, object?>)> _customSerializers
        = new();

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

    public ReadOnlyMemory<byte> Encode<T>(T obj, PacketFlags flags)
    {
        if (obj is null)
            throw new ArgumentNullException(nameof(obj));

        ReadOnlySpan<byte> dataSpan;

        if(_customSerializers.TryGetValue(obj.GetType(), out var customSerializer))
        {
            dataSpan = customSerializer.Item1(obj).Span;
        }
        else
        {
            dataSpan = MemoryPackSerializer.Serialize(obj).AsSpan();
        }

        if (dataSpan.Length + 8 > ushort.MaxValue)
            throw new InvalidOperationException($"Message too large [Length - {dataSpan.Length}]");

        var packetId = PacketIdMapper.GetPacketId(obj.GetType());

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

    public PacketDecodeResultWithId<ushort> Decode(ReadOnlySpan<byte> data)
    {
        // 负载长度
        // var packetLengthSpan = data[..2];

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

        if (_customSerializers.TryGetValue(PacketIdMapper.GetPacketType(packetId), out var customSerializer))
        {
            var deserializedPayload = customSerializer.Item2(packetData.ToArray());

            if (deserializedPayload is null)
                throw new InvalidOperationException($"Failed to deserialize packet with id {packetId}!");

            return new PacketDecodeResultWithId<ushort>(packetPrefixes, flags, packetId, deserializedPayload);
        }

        // var packetLength = BitConverter.ToUInt16(packetLengthSpan);
        var packetType = PacketIdMapper.GetPacketType(packetId);
        var payload = MemoryPackSerializer.Deserialize(packetType, packetData);

        return new PacketDecodeResultWithId<ushort>(packetPrefixes, flags, packetId, payload);
    }

    public void RegisterCustomSerializer<T>(Func<T, ReadOnlyMemory<byte>> serializer, Func<ReadOnlyMemory<byte>, T> deserializer){
        PacketIdMapper.GetPacketId(typeof(T));

        var serializerWrapper = new Func<object, ReadOnlyMemory<byte>>(obj => serializer((T)obj));
        var deserializerWrapper = new Func<ReadOnlyMemory<byte>, object?>(memory => deserializer(memory));

        _customSerializers.AddOrUpdate(
            typeof(T),
            (serializerWrapper, deserializerWrapper),
            (_, _) => (serializerWrapper, deserializerWrapper));
    }
}