﻿using System;

namespace Hive.Framework.Codec.Abstractions
{
    /// <summary>
    /// 封包编解码器接口
    /// </summary>
    /// <typeparam name="TId">封包 ID 类型（通常为 ushort）</typeparam>
    public interface IPacketCodec<TId>
    {
        IPacketIdMapper<TId> PacketIdMapper { get; }

        ReadOnlyMemory<byte> Encode<T>(T obj);

        object Decode(ReadOnlySpan<byte> data);
    }
}