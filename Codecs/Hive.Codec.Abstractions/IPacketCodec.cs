using System;
using Hive.Framework.Shared;

namespace Hive.Framework.Codec.Abstractions
{
    /// <summary>
    /// 封包编解码器接口
    /// </summary>
    /// <typeparam name="TId">封包 ID 类型（通常为 ushort）</typeparam>
    public interface IPacketCodec<TId> where TId : unmanaged
    {
        IPacketPrefixResolver[]? PrefixResolvers { get; }

        IPacketIdMapper<TId> PacketIdMapper { get; }

        ReadOnlyMemory<byte> GetPacketIdMemory(ReadOnlyMemory<byte> payload);
        TId GetPacketId(ReadOnlyMemory<byte> idMemory);

        ReadOnlyMemory<byte> GetPacketFlagsMemory(ReadOnlyMemory<byte> payload);
        PacketFlags GetPacketFlags(ReadOnlyMemory<byte> data);

        ReadOnlyMemory<byte> Encode<T>(T obj, PacketFlags flags);
        PacketDecodeResultWithId<TId> Decode(ReadOnlySpan<byte> data);

        void RegisterCustomSerializer<T>(Func<T, ReadOnlyMemory<byte>> serializer, Func<ReadOnlyMemory<byte>, T> deserializer);
    }
}