using System;

namespace Hive.Framework.Codec.Abstractions
{
    public interface IDecoder<TData>
    {
        T Decode<T>(ReadOnlySpan<TData> data) where T : unmanaged;
    }
}