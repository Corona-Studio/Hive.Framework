using System;
using System.Buffers;
using System.IO;
using Hive.Codec.Abstractions;
using Hive.Codec.Shared;
using ProtoBuf.Meta;

namespace Hive.Codec.Protobuf;

public class ProtoBufPacketCodec : AbstractPacketCodec
{
    public ProtoBufPacketCodec(IPacketIdMapper packetIdMapper, ICustomCodecProvider customCodecProvider) : base(
        packetIdMapper, customCodecProvider)
    {
    }

    protected override int EncodeBody<T>(T message, Stream stream)
    {
        return (int)RuntimeTypeModel.Default.Serialize(stream, message);
    }

    protected override object DecodeBody(ReadOnlySequence<byte> buffer, Type type)
    {
        using var stream = new ReadOnlySequenceStream(buffer);
        return RuntimeTypeModel.Default.Deserialize(stream, null, type);
    }
}