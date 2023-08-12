﻿using System;
using System.Buffers;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.Framework.Networking.Shared.Helpers;

public static class SessionExtensions
{
    /// <summary>
    /// 发送包含自定义前缀的数据包
    /// 使用该方法时会在标志位中添加 <see cref="PacketFlags.HasCustomPacketPrefix"/> 标志位
    /// <para>注意：在使用该方法时，请一定确保真正的写入了封包前缀，该方法并不支持忽略封包前缀</para>
    /// </summary>
    /// <typeparam name="TPayload">封包负载类型</typeparam>
    /// <typeparam name="TSender">发送者类型</typeparam>
    /// <typeparam name="TId">封包 ID 类型</typeparam>
    /// <param name="session">发送封包的会话</param>
    /// <param name="codec">封包编解码器</param>
    /// <param name="flags">封包标志位</param>
    /// <param name="payload">封包负载</param>
    /// <param name="prefixWriteAction">封包前缀写入回调</param>
    /// <returns></returns>
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

    /// <summary>
    /// 发送一个不包含负载的数据包
    /// 使用该方法时会在标志位中添加 <see cref="PacketFlags.NoPayload"/> 标志位
    /// </summary>
    /// <typeparam name="TSender">发送者类型</typeparam>
    /// <typeparam name="TId">封包 ID 类型</typeparam>
    /// <param name="session">发送封包的会话</param>
    /// <param name="codec">封包编解码器</param>
    /// <param name="flags">封包标志位</param>
    /// <param name="prefixWriteAction">封包前缀写入回调</param>
    /// <returns></returns>
    public static async ValueTask SendWithoutPayload<TSender, TId>(
        this ISession<TSender> session,
        IPacketCodec<TId> codec,
        PacketFlags flags,
        Action<IBufferWriter<byte>>? prefixWriteAction = null)
        where TSender : ISession<TSender>
        where TId : unmanaged
    {
        flags |= PacketFlags.NoPayload;

        var writer = new ArrayBufferWriter<byte>(20);

        // [LENGTH (2) | PACKET_FLAGS (4) | PACKET_ID | SESSION_ID | PAYLOAD]
        BitConverter.TryWriteBytes(writer.GetSpan(sizeof(uint)), (uint)flags);
        writer.Advance(sizeof(uint));

        prefixWriteAction?.Invoke(writer);

        var resultLength = BitConverter.GetBytes((ushort)writer.WrittenCount);
        var resultPacket = MemoryHelper.CombineMemory(resultLength, writer.WrittenMemory);

        await session.SendAsync(resultPacket);
    }
}