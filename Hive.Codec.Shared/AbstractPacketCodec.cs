using System;
using System.IO;
using Hive.Codec.Abstractions;

namespace Hive.Codec.Shared;

public abstract class AbstractPacketCodec : IPacketCodec
{
    private readonly ICustomCodecProvider _customCodecProvider;
    private readonly IPacketIdMapper _packetIdMapper;

    protected AbstractPacketCodec(IPacketIdMapper packetIdMapper, ICustomCodecProvider customCodecProvider)
    {
        _packetIdMapper = packetIdMapper;
        _customCodecProvider = customCodecProvider;
    }

    public int Encode<T>(T message, Stream writer)
    {
        var id = _packetIdMapper.GetPacketId(typeof(T));

        Span<byte> buffer = stackalloc byte[PacketId.Size];
        id.WriteTo(buffer);

        writer.Write(buffer);

        var packetCodec = _customCodecProvider.GetPacketCodec(id);
        var bodyLength = packetCodec?.EncodeBody(message, writer) ?? EncodeBody(message, writer);

        return PacketId.Size + bodyLength;
    }

    public object? Decode(ReadOnlyMemory<byte> bytes)
    {
        var packetId = PacketId.From(bytes.Span);
        var type = _packetIdMapper.GetPacketType(packetId);
        var packetCodec = _customCodecProvider.GetPacketCodec(packetId);

        var slice = bytes[PacketId.Size..];

        var body = packetCodec != null
            ? packetCodec.DecodeBody(slice, type)
            : DecodeBody(slice, type);

        return body;
    }

    protected abstract int EncodeBody<T>(T message, Stream stream);

    protected abstract object? DecodeBody(ReadOnlyMemory<byte> bytes, Type type);
}