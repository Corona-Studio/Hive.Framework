using System;

namespace Hive.Framework.Codec.Abstractions
{
    public readonly ref struct ResolveResultBase<TData, TResolveInfo>
    {
        /// <summary>
        /// 其他封包信息（例如长度、封包类型等）
        /// </summary>
        public readonly TResolveInfo ResolveInfo;

        /// <summary>
        /// 数据段
        /// </summary>
        public readonly ReadOnlySpan<TData> Data;

        public ResolveResultBase(TResolveInfo resolveInfo, ReadOnlySpan<TData> data)
        {
            ResolveInfo = resolveInfo;
            Data = data;
        }
    }
}