using System;
using System.Buffers;
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

    public object? Decode(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < PacketId.Size)
            throw new InvalidDataException($"Invalid packet id size: {buffer.Length}");

        Span<byte> headerBuffer = stackalloc byte[PacketId.Size];

        buffer.Slice(0, PacketId.Size).CopyTo(headerBuffer);

        var packetId = PacketId.From(headerBuffer);
        var type = _packetIdMapper.GetPacketType(packetId);
        var packetCodec = _customCodecProvider.GetPacketCodec(packetId);
        var bodySlice = buffer.Slice(PacketId.Size);

        return packetCodec != null
            ? packetCodec.DecodeBody(bodySlice, type)
            : DecodeBody(bodySlice, type);
    }

    protected abstract int EncodeBody<T>(T message, Stream stream);

    protected abstract object? DecodeBody(ReadOnlySequence<byte> buffer, Type type);
}