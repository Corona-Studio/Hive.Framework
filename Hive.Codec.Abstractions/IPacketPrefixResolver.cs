using System;

namespace Hive.Codec.Abstractions
{
    /// <summary>
    ///     封包前缀解析器
    ///     <para>该抽象用于解析介于封包长度段和负载段之间的数据，这些数据通常是由网关注入的</para>
    ///     <para>示例： [长度 | 前缀 1 | 前缀 2 | ... | 前缀 n | 负载]</para>
    /// </summary>
    public interface IPacketPrefixResolver
    {
        /// <summary>
        ///     解析封包前缀
        /// </summary>
        /// <param name="data">完整数据 [长度 | 前缀 1 | 前缀 2 | ... | 前缀 n | 负载]</param>
        /// <param name="index">
        ///     当前索引位置，传入索引的引用
        ///     <para>您需要在解析完对应的前缀数据段之后，更新 index 的值。</para>
        ///     <para>例如，您读取了一个长度为 2 的前缀，在跳出当前方法前，请将 index + 2</para>
        /// </param>
        /// <returns></returns>
        object Resolve(ReadOnlySpan<byte> data, ref int index);
    }
}