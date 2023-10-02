using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using System;
using System.Buffers;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using Hive.Framework.Shared.Helpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Collections.Concurrent;
using Hive.Codec.Shared;
using MongoDB.Bson.IO;

namespace Hive.Framework.Codec.Bson;

public class BsonPacketCodec : AbstractPacketCodec
{
    public BsonPacketCodec(IPacketIdMapper packetIdMapper, ICustomCodecProvider customCodecProvider) : base(packetIdMapper, customCodecProvider)
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