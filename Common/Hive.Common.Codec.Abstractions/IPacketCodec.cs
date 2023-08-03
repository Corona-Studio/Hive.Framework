using System;

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

        TId GetPacketId(ReadOnlyMemory<byte> idMemory);

        ReadOnlyMemory<byte> GetPacketIdMemory(ReadOnlyMemory<byte> payload);

        ReadOnlyMemory<byte> Encode<T>(T obj);

        PacketDecodeResultWithId<TId> Decode(ReadOnlySpan<byte> data);
    }
}