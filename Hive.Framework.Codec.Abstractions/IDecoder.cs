using System;

namespace Hive.Framework.Codec.Abstractions
{
    /// <summary>
    /// 封包解码器接口
    /// </summary>
    /// <typeparam name="TData">目标数据类型（通常为 byte）</typeparam>
    /// <typeparam name="TResolveResult"></typeparam>
    public interface IDecoder<TData, TResolveResult>
    {
        IPacketResolver<TData, TResolveResult> PacketResolver { get; }

        ResolveResultBase<TData, TResolveResult> ResolveData(ReadOnlySpan<TData> data);

        /// <summary>
        /// 解码实现（只应负责最纯粹的解码工作，即反序列化数据段）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        T Decode<T>(ReadOnlySpan<TData> data) where T : unmanaged;
    }
}