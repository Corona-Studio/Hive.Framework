﻿using System;
using System.Buffers;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Shared.Helpers;

public static class SessionExtensions
{
    public static async ValueTask SendWithPrefix<TPayload, TSender, TId>(
        this ISession<TSender> session,
        IPacketCodec<TId> codec,
        TPayload payload,
        Action<IBufferWriter<byte>> prefixWriteAction)
        where TSender : ISession<TSender>
        where TId : unmanaged
    {
        var encodedPayload = codec.Encode(payload);
        var writer = new ArrayBufferWriter<byte>();

        var packetIdMemory = codec.GetPacketIdMemory(encodedPayload);
        var actualPayloadMemory = encodedPayload[(2 + packetIdMemory.Length)..];

        // [LENGTH (2) | PACKET_ID | SESSION_ID | PAYLOAD]
        writer.Write(packetIdMemory.Span);
        prefixWriteAction(writer);
        writer.Write(actualPayloadMemory.Span);

        var resultLength = BitConverter.GetBytes((ushort)writer.WrittenCount);
        var resultPacket = MemoryHelper.CombineMemory(resultLength, writer.WrittenMemory);

        await session.Send(resultPacket);
    }
}