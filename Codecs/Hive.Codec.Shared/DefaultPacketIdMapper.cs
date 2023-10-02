using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared.Collections;
using System;
using System.Data;
using System.Reflection;
using System.Threading;
using Hive.Codec.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace Hive.Codec.Shared
{
    public class DefaultPacketIdMapper : IPacketIdMapper
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly BiDictionary<Type, PacketId> _typeIdMapping = new BiDictionary<Type, PacketId>();
        
        private readonly ILogger<DefaultPacketIdMapper> _logger;

        public DefaultPacketIdMapper(ILogger<DefaultPacketIdMapper> logger)
        {
            _logger = logger;
            ScanAll();
        }
        
        public void ScanAll()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                Scan(assembly);
            }
        }
        
        public void Scan(Assembly assembly)
        {
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                if (type.IsDefined(typeof(MessageDefineAttribute), false))
                {
                    Register(type);
                }
            }
        }

        public void Register<TPacket>() => Register(typeof(TPacket));

        public void Register(Type type) => Register(type, out _);
        public void Register(Type type, out PacketId id)
        {
            if(_lock.TryEnterWriteLock(10))
            {
                try
                {
                    if (_typeIdMapping.ContainsKey(type))
                        throw new DuplicateNameException($"Failed to register msg type {type}. You already registered it!");

                    var newId = TypeHashUtil.GetTypeHash(type);

                    if (_typeIdMapping.ContainsValue(newId))
                        throw new DuplicateNameException($"Failed to register msg type {type}. Duplicate id found [ID - {newId}]!");

                    _typeIdMapping.Add(type, newId);
                    id = newId;
                    _logger.LogInformation($"Registered msg type {type} with id {newId}");
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            else
            {
                throw new TimeoutException($"Failed to register msg type {type}. Timeout!");
            }
        }

        PacketId IPacketIdMapper.GetPacketId(Type type)
        {
            return GetPacketId(type);
        }

        public PacketId GetPacketId(Type type)
        {
            if(_lock.TryEnterReadLock(10))
            {
                try
                {
                    if (_typeIdMapping.TryGetValueByKey(type, out var id)) return id;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }

            throw new InvalidOperationException($"Cannot get id of msg type {type}");
        }
        public Type GetPacketType(PacketId id)
        {
            if(_lock.TryEnterReadLock(10))
            {
                try
                {
                    if (_typeIdMapping.TryGetKeyByValue(id, out var type)) return type;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }

            throw new InvalidOperationException($"Cannot get type of msg id {id}");
        }
    }
}