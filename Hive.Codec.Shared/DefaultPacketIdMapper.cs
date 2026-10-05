using System;
using System.Data;
using Hive.Codec.Abstractions;
using Hive.Codec.Shared.Helpers;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hive.Codec.Shared;

public class DefaultPacketIdMapper : IPacketIdMapper
{
    private readonly object _lock = new();

    private readonly ILogger<DefaultPacketIdMapper> _logger;
    private sealed class Mapping
    {
        public readonly Dictionary<Type, PacketId> Types;
        public readonly Dictionary<PacketId, Type> Ids;
        public Mapping(Dictionary<Type, PacketId> types, Dictionary<PacketId, Type> ids)
        {
            Types = types;
            Ids = ids;
        }
    }
    private Mapping _mapping = new(new Dictionary<Type, PacketId>(), new Dictionary<PacketId, Type>());

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
            var mapping = _mapping;
            if (mapping.Types.ContainsKey(type))
                throw new DuplicateNameException(
                    $"Failed to register msg type {type}. You already registered it!");

            var newId = TypeHashUtil.GetTypeHash(type);

            if (mapping.Ids.ContainsKey(newId))
                throw new DuplicateNameException(
                    $"Failed to register msg type {type}. Duplicate id found [ID - {newId}]!");

            var types = new Dictionary<Type, PacketId>(mapping.Types) { [type] = newId };
            var ids = new Dictionary<PacketId, Type>(mapping.Ids) { [newId] = type };
            Volatile.Write(ref _mapping, new Mapping(types, ids));
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
        if (Volatile.Read(ref _mapping).Ids.TryGetValue(id, out var type)) return type;

        throw new InvalidOperationException($"Cannot get type of msg id {id}");
    }

    public PacketId GetPacketId(Type type)
    {
        if (Volatile.Read(ref _mapping).Types.TryGetValue(type, out var id)) return id;

        throw new InvalidOperationException($"Cannot get id of msg type {type}");
    }
}

internal static partial class DefaultPacketIdMapperLoggers
{
    [LoggerMessage(LogLevel.Information, "Registered msg type {type} with id {newId}")]
    public static partial void RegisteredMsgType(this ILogger<DefaultPacketIdMapper> logger, Type type, PacketId newId);
}