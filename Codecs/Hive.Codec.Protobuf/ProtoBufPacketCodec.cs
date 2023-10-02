using Hive.Framework.Codec.Abstractions;
using System;
using System.Buffers;
using System.IO;
using ProtoBuf.Meta;
using Hive.Codec.Shared;

namespace Hive.Framework.Codec.Protobuf;

public class ProtoBufPacketCodec : AbstractPacketCodec
{
    public ProtoBufPacketCodec(IPacketIdMapper packetIdMapper, ICustomCodecProvider customCodecProvider) : base(packetIdMapper, customCodecProvider)
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