using Hive.Framework.Codec.Abstractions;
using MemoryPack;
using Microsoft.Extensions.ObjectPool;
using System.Buffers;
using Hive.Framework.Shared;

namespace Hive.Common.Codec.MemoryPack;

public class MemoryPackPacketCodec : IPacketCodec<ushort>
{
    private static readonly ObjectPool<ArrayBufferWriter<byte>> WriterPool;

    static MemoryPackPacketCodec()
    {
        WriterPool = new DefaultObjectPool<ArrayBufferWriter<byte>>(new BufferWriterPoolPolicy());
    }

    public MemoryPackPacketCodec(
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
            var objBytes = MemoryPackSerializer.Serialize(obj);

            if (objBytes.Length + 4 > ushort.MaxValue)
                throw new InvalidOperationException($"Message to large [Length - {objBytes.Length}]");

            var packetId = PacketIdMapper.GetPacketId(typeof(T));

            Span<byte> lengthHeader = stackalloc byte[2];
            Span<byte> typeHeader = stackalloc byte[2];

            // Packet Length [LENGTH (2) | TYPE (2) | CONTENT]
            BitConverter.TryWriteBytes(lengthHeader, (ushort)(objBytes.Length + 2));
            writer.Write(lengthHeader);

            // Packet Id
            BitConverter.TryWriteBytes(typeHeader, packetId);
            writer.Write(typeHeader);

            // Packet Load
            writer.Write(objBytes);

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

    public PacketDecodeResult<ushort> Decode(ReadOnlySpan<byte> data)
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
        var payload = MemoryPackSerializer.Deserialize(packetType, packetData);

        return new PacketDecodeResult<ushort>(packetPrefixes, packetId, payload);
    }
}