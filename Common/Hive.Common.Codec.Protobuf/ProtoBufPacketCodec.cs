using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared;
using ProtoBuf;
using System;
using System.Buffers;
using ProtoBuf.Meta;
using System.Linq;
using Microsoft.Extensions.ObjectPool;

namespace Hive.Framework.Codec.Protobuf;

public class ProtoBufPacketCodec : IPacketCodec<ushort>
{
    private static readonly ObjectPool<ArrayBufferWriter<byte>> WriterPool;

    static ProtoBufPacketCodec()
    {
        WriterPool = new DefaultObjectPool<ArrayBufferWriter<byte>>(new BufferWriterPoolPolicy());
    }

    public ProtoBufPacketCodec(
        IPacketIdMapper<ushort> packetIdMapper,
        IPacketPrefixResolver[]? prefixResolvers = null)
    {
        PacketIdMapper = packetIdMapper;
        PrefixResolvers = prefixResolvers;
    }
    
    public IPacketIdMapper<ushort> PacketIdMapper { get; }
    public IPacketPrefixResolver[]? PrefixResolvers { get; }

    public ReadOnlyMemory<byte> GetPacketIdMemory(ReadOnlyMemory<byte> payload)
    {
        if (payload.IsEmpty)
            throw new InvalidOperationException($"{nameof(payload)} has length of 0!");

        return payload.Slice(2, 2);
    }

    public ushort GetPacketId(ReadOnlyMemory<byte> idMemory)
    {
        return BitConverter.ToUInt16(idMemory.Span);
    }

    public ReadOnlyMemory<byte> Encode<T>(T obj)
    {
        var writer = WriterPool.Get();

        try
        {
            using var contentMeasure = Serializer.Measure(obj);

            if (contentMeasure.Length + 4 > ushort.MaxValue)
                throw new InvalidOperationException($"Message to large [Length - {contentMeasure.Length}]");

            var packetId = PacketIdMapper.GetPacketId(typeof(T));

            Span<byte> lengthHeader = stackalloc byte[2];
            Span<byte> typeHeader = stackalloc byte[2];

            // Packet Length [LENGTH (2) | TYPE (2) | CONTENT]
            BitConverter.TryWriteBytes(lengthHeader, (ushort)(contentMeasure.Length + 2));
            writer.Write(lengthHeader);

            // Packet Id
            BitConverter.TryWriteBytes(typeHeader, packetId);
            writer.Write(typeHeader);

            contentMeasure.Serialize(writer);

            return writer.WrittenMemory.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
        finally
        {
            WriterPool.Return(writer);
        }
    }

    public PacketDecodeResultWithId<ushort> Decode(ReadOnlySpan<byte> data)
    {
        // 负载长度
        // var packetLengthSpan = data[..2];

        // 封包类型
        var packetIdSpan = data.Slice(2, 2);
        var packetId = BitConverter.ToUInt16(packetIdSpan);

        // 封包前缀
        var payloadStartIndex = 4;
        var packetPrefixes = Array.Empty<object?>();
        if (PrefixResolvers?.Any() ?? false)
        {
            packetPrefixes = new object[PrefixResolvers.Length];
            for (var i = 0; i < PrefixResolvers.Length; i++)
            {
                packetPrefixes[i] = PrefixResolvers[i].Resolve(data, ref payloadStartIndex);
            }
        }

        // 封包数据段
        var packetData = data[payloadStartIndex..];

        // var packetLength = BitConverter.ToUInt16(packetLengthSpan);
        var packetType = PacketIdMapper.GetPacketType(packetId);
        var payload = RuntimeTypeModel.Default.Deserialize(packetType, packetData);

        return new PacketDecodeResultWithId<ushort>(packetPrefixes, packetId, payload);
    }
}