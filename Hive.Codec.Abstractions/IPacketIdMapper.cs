using System;

namespace Hive.Codec.Abstractions
{
    /// <summary>
    /// 封包 类型 / ID 映射器
    /// </summary>
    public interface IPacketIdMapper
    {
        void Register<TPacket>();
        void Register(Type type);
        void Register(Type type, out PacketId id);

        PacketId GetPacketId(Type type);
        Type GetPacketType(PacketId id);
    }
}