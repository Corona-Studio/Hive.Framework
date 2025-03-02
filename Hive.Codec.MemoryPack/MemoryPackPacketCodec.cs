using Hive.Codec.Abstractions;
using Hive.Codec.Shared;
using MemoryPack;
using System.Buffers;

namespace Hive.Codec.MemoryPack;

public class MemoryPackPacketCodec : AbstractPacketCodec
{
    public MemoryPackPacketCodec(IPacketIdMapper packetIdMapper, ICustomCodecProvider customCodecProvider) : base(
        packetIdMapper, customCodecProvider)
    {
    }

    protected override int EncodeBody<T>(T message, Stream stream)
    {
        var bytes = MemoryPackSerializer.Serialize(message).AsSpan();

        stream.Write(bytes);

        return bytes.Length;
    }

    protected override object? DecodeBody(ReadOnlySequence<byte> buffer, Type type)
    {
        return MemoryPackSerializer.Deserialize(type, buffer);
    }
}