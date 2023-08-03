using System;

namespace Hive.Framework.Networking.Abstractions;

public interface IServerReplyPacket<out TId> where TId : unmanaged
{
    TId SendTo { get; }
    ReadOnlyMemory<byte> InnerPayload { get; }
}