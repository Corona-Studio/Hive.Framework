using System;
using System.Data;
using System.Reflection;
using System.Threading;
using Hive.Codec.Abstractions;
using Hive.Codec.Shared.Helpers;
using Hive.Common.Shared.Collections;
using Microsoft.Extensions.Logging;

namespace Hive.Codec.Shared
{
    public class DefaultPacketIdMapper : IPacketIdMapper
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        private readonly ILogger<DefaultPacketIdMapper> _logger;
        private readonly BiDictionary<Type, PacketId> _typeIdMapping = new BiDictionary<Type, PacketId>();

        public DefaultPacketIdMapper(ILogger<DefaultPacketIdMapper> logger)
        {
            _logger = logger;
            ScanAll();
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
            if (_lock.TryEnterWriteLock(10))
                try
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
                finally
                {
                    _lock.ExitWriteLock();
                }
            else
                throw new TimeoutException($"Failed to register msg type {type}. Timeout!");
        }

        PacketId IPacketIdMapper.GetPacketId(Type type)
        {
            return GetPacketId(type);
        }

        public Type GetPacketType(PacketId id)
        {
            if (_lock.TryEnterReadLock(10))
                try
                {
                    if (_typeIdMapping.TryGetKeyByValue(id, out var type)) return type;
                }
                finally
                {
                    _lock.ExitReadLock();
                }

            throw new InvalidOperationException($"Cannot get type of msg id {id}");
        }

        public void ScanAll()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) Scan(assembly);
        }

        public void Scan(Assembly assembly)
        {
            var types = assembly.GetTypes();
            foreach (var type in types)
                if (type.IsDefined(typeof(MessageDefineAttribute), false))
                    Register(type);
        }

        public PacketId GetPacketId(Type type)
        {
            if (_lock.TryEnterReadLock(10))
                try
                {
                    if (_typeIdMapping.TryGetValueByKey(type, out var id)) return id;
                }
                finally
                {
                    _lock.ExitReadLock();
                }

            throw new InvalidOperationException($"Cannot get id of msg type {type}");
        }
    }

    internal static partial class DefaultPacketIdMapperLoggers
    {
        [LoggerMessage(LogLevel.Information, "Registered msg type {type} with id {newId}")]
        public static partial void RegisteredMsgType(this ILogger<DefaultPacketIdMapper> logger, Type type, PacketId newId);
    }
}