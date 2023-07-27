using System;

namespace Hive.Framework.Codec.Abstractions
{
    public interface ITypeHashFunction<out THash> where THash : struct
    {
        THash GetTypeHash(Type type);
    }
}