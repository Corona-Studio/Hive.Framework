using Hive.Framework.Codec.Abstractions;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Hive.Common.Codec.MemoryPack;

public class MemoryPackPacketIdMapper : IPacketIdMapper<ushort>
{
    private const ushort HashMode = 65521; // 小于 65535 的质数

    private readonly Dictionary<Type, ushort> _typeIdMapping = new();
    private readonly Dictionary<ushort, Type> _idTypeMapping = new();

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
        if (_typeIdMapping.ContainsKey(type))
            throw new DuplicateNameException($"Failed to register msg type {type}. You already registered it!");

        var newId = GetIdHash(type);

        if (_idTypeMapping.ContainsKey(newId))
            throw new DuplicateNameException($"Failed to register msg type {type}. Duplicate id found [ID - {newId}]!");

        _typeIdMapping[type] = newId;
        _idTypeMapping[newId] = type;

        id = newId;
    }

    public ushort GetPacketId(Type type)
    {
        if (_typeIdMapping.TryGetValue(type, out var id)) return id;

        throw new ArgumentOutOfRangeException($"Cannot get id of msg type {type}");
    }

    public Type GetPacketType(ushort id)
    {
        if (_idTypeMapping.TryGetValue(id, out var type)) return type;

        throw new ArgumentOutOfRangeException($"Cannot get type of msg id {id}");
    }
}