using System;
using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Codec.Protobuf;

/// <summary>
/// ProtoBuf 的封包解析器默认实现
/// </summary>
public class ProtoBufPacketResolver : IPacketResolver<byte, ProtoBufResolveInfo>
{
    /// <summary>
    /// 解析实现
    /// 字节流 [ 负载长度2B (ushort) | 负载类型1B (byte) | 负载数据 ]
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public ResolveResultBase<byte, ProtoBufResolveInfo> Resolve(ReadOnlySpan<byte> data)
    {
        // 负载长度
        var packetLengthSpan = data[..2];

        // 封包类型
        var packetId = data[2];

        // 封包数据段
        var packetData = data[2..];

        var packetLength = BitConverter.ToUInt16(packetLengthSpan);

        return new ResolveResultBase<byte, ProtoBufResolveInfo>(new ProtoBufResolveInfo(packetLength, packetId), packetData);
    }
}