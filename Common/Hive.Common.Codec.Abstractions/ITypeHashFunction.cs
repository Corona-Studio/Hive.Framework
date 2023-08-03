using System;

namespace Hive.Framework.Codec.Abstractions
{
    public interface ITypeHashFunction<out THash> where THash : unmanaged
    {
        THash GetTypeHash(Type type);
    }
}