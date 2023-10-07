using System;
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

    protected override object DecodeBody(Stream stream, Type type)
    {
        return RuntimeTypeModel.Default.Deserialize(stream, null, type);
    }
}