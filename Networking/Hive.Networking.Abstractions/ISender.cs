using System;
using System.Threading.Tasks;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Abstractions
{
    /// <summary>
    /// 表示一个发送者
    /// </summary>
    public interface ISender
    {
        /// <summary>
        /// 发送一个对象，将使用编码器编码后使用 <see cref="SendAsync"/> 加入到发送队列
        /// </summary>
        /// <param name="obj">待编码的对象</param>
        /// <param name="flags">封包标志</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        ValueTask SendAsync<T>(T obj, PacketFlags flags);
        
        /// <summary>
        /// 将原始数据流加入发送队列
        /// </summary>
        /// <param name="data">待加入队列的数据</param>
        /// <returns></returns>
        ValueTask SendAsync(ReadOnlyMemory<byte> data);
        
        /// <summary>
        /// 发送方具体发送实现
        /// </summary>
        /// <param name="data">待发送的数据</param>
        /// <returns></returns>
        ValueTask SendOnce(ReadOnlyMemory<byte> data);
    }
}