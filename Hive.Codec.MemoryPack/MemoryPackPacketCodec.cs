using Hive.Codec.Abstractions;
using Hive.Codec.Shared;
using MemoryPack;
using System.Buffers;

namespace Hive.Codec.MemoryPack;

public class MemoryPackPacketCodec(
    IPacketIdMapper packetIdMapper,
    ICustomCodecProvider customCodecProvider)
    : AbstractPacketCodec(packetIdMapper, customCodecProvider)
{
    protected override int EncodeBody<T>(T message, Stream stream)
    {
        if (stream is IBufferWriter<byte> bufferWriter)
        {
            var position = stream.Position;

            MemoryPackSerializer.Serialize(bufferWriter, message);

            return (int)(stream.Position - position);
        }
        
        var bytes = MemoryPackSerializer.Serialize(message).AsSpan();

        stream.Write(bytes);

        return bytes.Length;
    }

    protected override object? DecodeBody(ReadOnlySequence<byte> buffer, Type type)
    {
        return MemoryPackSerializer.Deserialize(type, buffer);
    }
}