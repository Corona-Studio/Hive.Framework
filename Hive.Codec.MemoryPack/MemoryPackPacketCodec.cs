using Hive.Codec.Abstractions;
using Hive.Codec.Shared;
using MemoryPack;

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

    protected override object? DecodeBody(ReadOnlyMemory<byte> bytes, Type type)
    {
        return MemoryPackSerializer.Deserialize(type, bytes.Span);
    }
}