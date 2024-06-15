using System;

namespace Hive.Codec.Shared;

public class MessageDefineAttribute : Attribute
{
    /// <summary>
    ///     消息类型定义
    /// </summary>
    public MessageDefineType Type { get; }
}

[Flags]
public enum MessageDefineType : byte
{
    ClientToServer,
    ServerToClient,
    ServerToServer,
    ClientToClient
}