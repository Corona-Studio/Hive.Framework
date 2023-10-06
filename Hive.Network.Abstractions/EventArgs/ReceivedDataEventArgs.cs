using System;

namespace Hive.Network.Abstractions.EventArgs;

/// <summary>
/// 数据接收事件参数
/// </summary>
public class ReceivedDataEventArgs : System.EventArgs
{
    /// <summary>
    /// 封包 ID 的内存数据，如果该包是无负载包，该字段将为 <see cref="ReadOnlyMemory{byte}.Empty"/>
    /// </summary>
    public ReadOnlyMemory<byte> Id { get; }
    /// <summary>
    /// 封包的完整内存数据
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    public ReceivedDataEventArgs(ReadOnlyMemory<byte> id, ReadOnlyMemory<byte> data)
    {
        Id = id;
        Data = data;
    }
}