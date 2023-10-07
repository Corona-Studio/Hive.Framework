using System;
using System.IO;
using Hive.Codec.Abstractions;
using Hive.Codec.Shared;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Hive.Codec.Bson;

public class BsonPacketCodec : AbstractPacketCodec
{
    public BsonPacketCodec(IPacketIdMapper packetIdMapper, ICustomCodecProvider customCodecProvider) : base(
        packetIdMapper, customCodecProvider)
    {
    }

    protected override int EncodeBody<T>(T message, Stream stream)
    {
        var bsonWriter = new BsonBinaryWriter(stream);
        BsonSerializer.Serialize(bsonWriter, message);
        return (int)bsonWriter.Position;
    }

    protected override object? DecodeBody(Stream stream, Type type)
    {
        return BsonSerializer.Deserialize(stream, type);
    }
}