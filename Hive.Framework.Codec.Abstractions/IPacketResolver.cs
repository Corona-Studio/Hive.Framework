using System;

namespace Hive.Framework.Codec.Abstractions
{
    public interface IPacketResolver<TData, TResolveInfo>
    {
        ResolveResultBase<TData, TResolveInfo> Resolve(ReadOnlySpan<TData> data);
    }
}