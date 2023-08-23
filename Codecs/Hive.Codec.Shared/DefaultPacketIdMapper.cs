using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Shared.Collections;
using System;
using System.Data;
using Hive.Codec.Shared.TypeHashFunctions;

namespace Hive.Codec.Shared
{
    public class DefaultPacketIdMapper : IPacketIdMapper<ushort>
    {
        private readonly object _locker = new object();
        private readonly BiDictionary<Type, ushort> _typeIdMapping = new BiDictionary<Type, ushort>();

        public ITypeHashFunction<ushort> TypeHashFunction { get; } = new UShortTypeHashFunction();

        public void Register<TPacket>() => Register(typeof(TPacket));

        public void Register(Type type) => Register(type, out _);

        public void Register(Type type, out ushort id)
        {
            lock (_locker)
            {
                if (_typeIdMapping.ContainsKey(type))
                    throw new DuplicateNameException($"Failed to register msg type {type}. You already registered it!");

                var newId = TypeHashFunction.GetTypeHash(type);

                if (_typeIdMapping.ContainsValue(newId))
                    throw new DuplicateNameException($"Failed to register msg type {type}. Duplicate id found [ID - {newId}]!");

                _typeIdMapping.Add(type, newId);
                id = newId;
            }
        }

        public ushort GetPacketId(Type type)
        {
            lock (_locker)
            {
                if (_typeIdMapping.TryGetValueByKey(type, out var id)) return id;
            }

            throw new InvalidOperationException($"Cannot get id of msg type {type}");
        }

        public ReadOnlyMemory<byte> GetPacketIdMemory(Type type)
        {
            return BitConverter.GetBytes(GetPacketId(type));
        }

        public Type GetPacketType(ushort id)
        {
            lock (_locker)
            {
                if (_typeIdMapping.TryGetKeyByValue(id, out var type)) return type;
            }

            throw new InvalidOperationException($"Cannot get type of msg id {id}");
        }
    }
}