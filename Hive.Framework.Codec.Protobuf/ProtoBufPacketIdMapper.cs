using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Codec.Protobuf;

public class ProtoBufPacketIdMapper : IPacketIdMapper<byte>
{
    private const byte HashMode = 251; // 小于 255 的质数

    private readonly Dictionary<Type, byte> _typeIdMapping = new();
    private readonly Dictionary<byte, Type> _idTypeMapping = new();

    private static byte GetIdHash(Type type)
    {
        var typeName = type.FullName;

        if (typeName == null)
            throw new ArgumentException($"Failed to register type {type}");

        var hashCode = BitConverter.ToUInt64(MD5.HashData(Encoding.ASCII.GetBytes(typeName)));
        var id = (byte)(hashCode % HashMode);

        return id;
    }

    public void Register(Type type) => Register(type, out _);

    public void Register(Type type, [UnscopedRef] out byte id)
    {
        if (_typeIdMapping.ContainsKey(type))
            throw new DuplicateNameException($"Failed to register msg type {type}. You already registered it!");

        var newId = GetIdHash(type);

        if(_idTypeMapping.ContainsKey(newId))
            throw new DuplicateNameException($"Failed to register msg type {type}. Duplicate id found [ID - {newId}]!");

        _typeIdMapping[type] = newId;
        _idTypeMapping[newId] = type;

        id = newId;
    }

    public byte GetPacketId(Type type)
    {
        if (_typeIdMapping.TryGetValue(type, out var id)) return id;

        throw new ArgumentOutOfRangeException($"Cannot get id of msg type {type}");
    }

    public Type GetPacketType(byte id)
    {
        if (_idTypeMapping.TryGetValue(id, out var type)) return type;

        throw new ArgumentOutOfRangeException($"Cannot get type of msg id {id}");
    }
}