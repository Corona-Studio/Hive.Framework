using System;
using System.Security.Cryptography;
using System.Text;
using Hive.Framework.Codec.Abstractions;

namespace Hive.Codec.Shared.TypeHashFunctions
{
    public class UShortTypeHashFunction : ITypeHashFunction<ushort>
    {
        private const ushort HashMod = 65521; // 小于 65535 的质数

        public ushort GetTypeHash(Type type)
        {
            var typeName = type.FullName;

            if (typeName == null)
                throw new ArgumentException($"Failed to register type {type}");

            using var md5 = MD5.Create();
            var hashCode = BitConverter.ToUInt64(md5.ComputeHash(Encoding.ASCII.GetBytes(typeName)));
            var id = (ushort)(hashCode % HashMod);

            return id;
        }
    }
}