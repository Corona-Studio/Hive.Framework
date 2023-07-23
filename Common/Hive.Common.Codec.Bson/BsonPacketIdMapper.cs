using Hive.Framework.Codec.Abstractions;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System;
using Hive.Framework.Shared.Collections;

namespace Hive.Framework.Codec.Bson;

public class BsonPacketIdMapper : IPacketIdMapper<ushort>
{
    private const ushort HashMode = 65521; // 小于 65535 的质数

    private readonly object _locker = new ();
    private readonly BiDictionary<Type, ushort> _typeIdMapping = new();

    private static ushort GetIdHash(Type type)
    {
        var typeName = type.FullName;

        if (typeName == null)
            throw new ArgumentException($"Failed to register type {type}");

        var hashCode = BitConverter.ToUInt64(MD5.HashData(Encoding.ASCII.GetBytes(typeName)));
        var id = (ushort)(hashCode % HashMode);

        return id;
    }

    public void Register<TPacket>() => Register(typeof(TPacket));

    public void Register(Type type) => Register(type, out _);

    public void Register(Type type, [UnscopedRef] out ushort id)
    {
        lock (_locker)
        {
            if (_typeIdMapping.ContainsKey(type))
                throw new DuplicateNameException($"Failed to register msg type {type}. You already registered it!");

            var newId = GetIdHash(type);

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

    public Type GetPacketType(ushort id)
    {
        lock (_locker)
        {
            if (_typeIdMapping.TryGetKeyByValue(id, out var type)) return type;
        }

        throw new InvalidOperationException($"Cannot get type of msg id {id}");
    }
}