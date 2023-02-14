using System;
using System.Buffers;
using Hive.Framework.Codec.Abstractions;
using ProtoBuf;

namespace Hive.Framework.Codec.Protobuf;

/// <summary>
/// 默认的 ProtoBuf 封包生成器
/// </summary>
public class ProtoBufPacketGenerator : IPacketGenerator<byte>
{
    public IPacketIdMapper<byte> PacketIdMapper { get; init; } = null!;

    /// <summary>
    /// 封包格式
    /// 字节流 [ 负载长度2B (ushort) | 负载类型1B (byte) | 负载数据 ]
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <param name="writer"></param>
    public void Generate<T>(T obj, IBufferWriter<byte> writer) where T : unmanaged
    {
        var contentMeasure = Serializer.Measure(obj);

        if (contentMeasure.Length > ushort.MaxValue)
            throw new InvalidOperationException($"Message to large [Length - {contentMeasure.Length}]");

        var packetId = PacketIdMapper.GetPacketId(typeof(T));

        Span<byte> header = stackalloc byte[3];

        // Packet Length
        BitConverter.TryWriteBytes(header, (ushort)contentMeasure.Length);
        // Packet Id
        header[2] = packetId;

        writer.Write(header);
        contentMeasure.Serialize(writer);
    }
}