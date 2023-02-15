using System;

namespace Hive.Framework.Codec.Abstractions
{
    /// <summary>
    /// 封包解码器接口
    /// </summary>
    /// <typeparam name="TId">数据包 ID 类型（通常为 ushort）</typeparam>
    public interface IDecoder<TId>
    {
        IPacketIdMapper<TId> PacketIdMapper { get; }

        object Decode(ReadOnlySpan<byte> data);
    }
}