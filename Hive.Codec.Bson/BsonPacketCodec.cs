using System;
using System.Buffers;
using System.IO;
using CommunityToolkit.HighPerformance;
using Hive.Codec.Abstractions;
using Hive.Codec.Shared;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Hive.Codec.Bson;

public class BsonPacketCodec(
    IPacketIdMapper packetIdMapper,
    ICustomCodecProvider customCodecProvider)
    : AbstractPacketCodec(packetIdMapper, customCodecProvider)
{
    protected override int EncodeBody<T>(T message, Stream stream)
    {
        var bsonWriter = new BsonBinaryWriter(stream);
        BsonSerializer.Serialize(bsonWriter, message);
        return (int)bsonWriter.Position;
    }

    protected override object? DecodeBody(ReadOnlySequence<byte> buffer, Type type)
    {
        using var ms = buffer.AsStream();

        return BsonSerializer.Deserialize(ms, type);
    }
}