using System;

namespace Hive.Framework.Codec.Abstractions
{
    /// <summary>
    /// 封包 类型 / ID 映射器
    /// </summary>
    /// <typeparam name="TId">数据包 ID 类型（通常为 ushort）</typeparam>
    public interface IPacketIdMapper<TId> where TId : unmanaged
    {
        ITypeHashFunction<TId> TypeHashFunction { get; }

        void Register<TPacket>();
        void Register(Type type);
        void Register(Type type, out TId id);

        TId GetPacketId(Type type);
        ReadOnlyMemory<byte> GetPacketIdMemory(Type type);
        Type GetPacketType(TId id);
    }
}