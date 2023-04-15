using System;

namespace Hive.Framework.Networking.Abstractions.EventArgs;

/// <summary>
/// 数据接收事件参数
/// </summary>
public class ReceivedDataEventArgs : System.EventArgs
{
    public ReadOnlyMemory<byte> Id { get; }
    public ReadOnlyMemory<byte> Data { get; }

    public ReceivedDataEventArgs(ReadOnlyMemory<byte> id, ReadOnlyMemory<byte> data)
    {
        Id = id;
        Data = data;
    }
}