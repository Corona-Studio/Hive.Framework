using System;
using Hive.Framework.Codec.Abstractions;
using ProtoBuf;

namespace Hive.Framework.Codec.Protobuf;

public class ProtoBufDecoder : IDecoder<ushort>
{
    public IPacketIdMapper<ushort> PacketIdMapper { get; init; } = null!;

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