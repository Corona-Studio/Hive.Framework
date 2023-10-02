using System.Buffers;
using Hive.Framework.Codec.Abstractions;
using MemoryPack;
using Hive.Codec.Shared;
using Hive.Codec.Shared.Helpers;

namespace Hive.Codec.MemoryPack;

public class MemoryPackPacketCodec : AbstractPacketCodec
{
    public MemoryPackPacketCodec(IPacketIdMapper packetIdMapper, ICustomCodecProvider customCodecProvider) : base(packetIdMapper, customCodecProvider)
    {
    }

    protected override int EncodeBody<T>(T message, Stream stream)
    {
        using var bufferWriter = new StreamBufferWriter(stream);
        MemoryPackSerializer.Serialize(message.GetType(),bufferWriter, message);
        bufferWriter.Flush();
        return bufferWriter.WrittenCount;
    }

    protected override object? DecodeBody(Stream stream, Type type)
    {
        var dataLength = (int)stream.Length - (int)stream.Position;
        using var streamReader = new StreamBufferReader(stream);

        var readSpan = streamReader.Read();
        if (readSpan.Length != dataLength)
        {
            throw new InvalidDataException($"Invalid packet id size: {readSpan.Length}");
        }
        return MemoryPackSerializer.Deserialize(type, readSpan, null);
    }
}