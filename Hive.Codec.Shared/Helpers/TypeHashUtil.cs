using System;
using System.Security.Cryptography;
using System.Text;
using Hive.Codec.Abstractions;

namespace Hive.Codec.Shared.Helpers;

public static class TypeHashUtil
{
    private const ushort HashMod = 65521; // 小于 65535 的质数

    public static PacketId GetTypeHash(Type type)
    {
        var typeName = type.FullName ?? throw new ArgumentException($"Failed to register type {type}");

        var bytes = Encoding.ASCII.GetBytes(typeName);
#if NET5_0_OR_GREATER
        var hash = MD5.HashData(bytes);
#else
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(bytes);
#endif
        var hashCode = BitConverter.ToUInt64(hash);
        var id = (ushort)(hashCode % HashMod);

        return id;
    }
}