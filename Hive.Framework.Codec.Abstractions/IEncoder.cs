using System;

namespace Hive.Framework.Codec.Abstractions
{
    /// <summary>
    /// 封包编码器接口
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public interface IEncoder<TData>
    {
        IPacketGenerator<TData> PacketGenerator { get; }

        ReadOnlySpan<TData> Encode<T>(T obj) where T : unmanaged;
    }
}