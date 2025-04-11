using System;
using System.Buffers;
using System.IO;
using CommunityToolkit.HighPerformance;
using Hive.Codec.Abstractions;
using Hive.Codec.Shared;
using ProtoBuf.Meta;

namespace Hive.Codec.Protobuf;

public class ProtoBufPacketCodec(
    IPacketIdMapper packetIdMapper,
    ICustomCodecProvider customCodecProvider)
    : AbstractPacketCodec(packetIdMapper, customCodecProvider)
{
    protected override int EncodeBody<T>(T message, Stream stream)
    {
        return (int)RuntimeTypeModel.Default.Serialize(stream, message);
    }

    protected override object DecodeBody(ReadOnlySequence<byte> buffer, Type type)
    {
        using var stream = buffer.AsStream();
        return RuntimeTypeModel.Default.Deserialize(stream, null, type);
    }
}