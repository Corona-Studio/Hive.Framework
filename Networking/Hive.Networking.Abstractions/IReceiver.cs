using System;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions;

/// <summary>
/// 表示一个接收者
/// </summary>
public interface IReceiver
{
    /// <summary>
    /// 接收方具体接收实现
    /// </summary>
    /// <param name="buffer">接收数据缓存</param>
    /// <returns></returns>
    ValueTask<int> ReceiveOnce(Memory<byte> buffer);
}