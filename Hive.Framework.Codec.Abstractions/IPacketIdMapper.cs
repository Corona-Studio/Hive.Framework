using System;

namespace Hive.Framework.Codec.Abstractions
{
    /// <summary>
    /// 封包 类型 / ID 映射器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IPacketIdMapper<T>
    {
        void Register(Type type);
        void Register(Type type, out T id);

        T GetPacketId(Type type);
        Type GetPacketType(T id);
    }
}