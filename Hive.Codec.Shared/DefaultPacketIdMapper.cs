using System;
using System.Data;
using Hive.Codec.Abstractions;
using Hive.Codec.Shared.Helpers;
using Hive.Common.Shared.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hive.Codec.Shared;

public class DefaultPacketIdMapper : IPacketIdMapper
{
    private readonly object _lock = new();

    private readonly ILogger<DefaultPacketIdMapper> _logger;
    private readonly BiDictionary<Type, PacketId> _typeIdMapping = new();

    public DefaultPacketIdMapper(
        IOptions<PacketIdMapperOptions> registerOptions,
        ILogger<DefaultPacketIdMapper> logger)
    {
        _logger = logger;

        foreach (var packetType in registerOptions.Value.RegisteredPackets)
            Register(packetType);
    }

    public void Register<TPacket>()
    {
        Register(typeof(TPacket));
    }

    public void Register(Type type)
    {
        Register(type, out _);
    }

    public void Register(Type type, out PacketId id)
    {
        lock (_lock)
        {
            if (_typeIdMapping.ContainsKey(type))
                throw new DuplicateNameException(
                    $"Failed to register msg type {type}. You already registered it!");

            var newId = TypeHashUtil.GetTypeHash(type);

            if (_typeIdMapping.ContainsValue(newId))
                throw new DuplicateNameException(
                    $"Failed to register msg type {type}. Duplicate id found [ID - {newId}]!");

            _typeIdMapping.Add(type, newId);
            id = newId;

            _logger.RegisteredMsgType(type, newId);
        }
    }

    PacketId IPacketIdMapper.GetPacketId(Type type)
    {
        return GetPacketId(type);
    }

    public Type GetPacketType(PacketId id)
    {
        lock (_lock)
        {
            if (_typeIdMapping.TryGetKeyByValue(id, out var type)) return type;
        }

        throw new InvalidOperationException($"Cannot get type of msg id {id}");
    }

    public PacketId GetPacketId(Type type)
    {
        lock (_lock)
        {
            if (_typeIdMapping.TryGetValueByKey(type, out var id)) return id;
        }

        throw new InvalidOperationException($"Cannot get id of msg type {type}");
    }
}

internal static partial class DefaultPacketIdMapperLoggers
{
    [LoggerMessage(LogLevel.Information, "Registered msg type {type} with id {newId}")]
    public static partial void RegisteredMsgType(this ILogger<DefaultPacketIdMapper> logger, Type type, PacketId newId);
}