using System;
using System.Buffers;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.Framework.Networking.Shared.Helpers;

public static class SessionExtensions
{
    public static async ValueTask SendWithPrefix<TPayload, TSender, TId>(
        this ISession<TSender> session,
        IPacketCodec<TId> codec,
        PacketFlags flags,
        TPayload payload,
        Action<IBufferWriter<byte>> prefixWriteAction)
        where TSender : ISession<TSender>
        where TId : unmanaged
    {
        flags |= PacketFlags.HasCustomPacketPrefix;

        using var encodedPayload = codec.Encode(payload, flags);
        var writer = new ArrayBufferWriter<byte>();

        var packetFlagsMemory = codec.GetPacketFlagsMemory(encodedPayload.MemoryOwner.Memory);
        var packetIdMemory = codec.GetPacketIdMemory(encodedPayload.MemoryOwner.Memory);

        var payloadStartIndex = 2 + packetFlagsMemory.Length + packetIdMemory.Length;
        var actualPayloadMemory = encodedPayload.MemoryOwner.Memory[payloadStartIndex..encodedPayload.Length];

        // [LENGTH (2) | PACKET_FLAGS (4) | PACKET_ID | SESSION_ID | PAYLOAD]
        writer.Write(packetFlagsMemory.Span);
        writer.Write(packetIdMemory.Span);
        prefixWriteAction(writer);
        writer.Write(actualPayloadMemory.Span);

        var resultLength = BitConverter.GetBytes((ushort)writer.WrittenCount);
        var resultPacket = MemoryHelper.CombineMemory(resultLength, writer.WrittenMemory);

        await session.SendAsync(resultPacket);
    }
}