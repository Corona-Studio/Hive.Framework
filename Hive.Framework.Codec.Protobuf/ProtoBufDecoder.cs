using System;
using Hive.Framework.Codec.Abstractions;
using ProtoBuf;

namespace Hive.Framework.Codec.Protobuf;

public class ProtoBufDecoder : IDecoder<byte, ProtoBufResolveInfo>
{
    public IPacketResolver<byte, ProtoBufResolveInfo> PacketResolver { get; init; } = null!;

    public ResolveResultBase<byte, ProtoBufResolveInfo> ResolveData(ReadOnlySpan<byte> data)
    {
        return PacketResolver.Resolve(data);
    }

    public T Decode<T>(ReadOnlySpan<byte> data) where T : unmanaged
    {
        return Serializer.Deserialize<T>(data);
    }
}