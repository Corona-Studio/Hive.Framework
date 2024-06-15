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
        var bodyLength = 0;
        if (packetCodec != null)
            bodyLength = packetCodec.EncodeBody(message, writer);
        else
            bodyLength = EncodeBody(message, writer);

        return PacketId.Size + bodyLength;
    }

    public object? Decode(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[PacketId.Size];

        var read = stream.Read(buffer);
        if (read != PacketId.Size) throw new InvalidDataException($"Invalid packet id size: {read}");
        var packetId = PacketId.From(buffer);

        var type = _packetIdMapper.GetPacketType(packetId);
        var packetCodec = _customCodecProvider.GetPacketCodec(packetId);
        object? body = null;
        if (packetCodec != null)
            body = packetCodec.DecodeBody(stream, type);
        else
            body = DecodeBody(stream, type);
        return body;
    }

    protected abstract int EncodeBody<T>(T message, Stream stream);

    protected abstract object? DecodeBody(Stream stream, Type type);
}