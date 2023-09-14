using Hive.DataSync.Abstraction.Interfaces;
using Hive.DataSync.Shared.ObjectSyncPacket;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;

namespace Hive.DataSync.Shared.Helpers
{
    public static class SyncPacketHelper
    {
        public static void RegisterSyncPacketPacketIds<TId>(this IPacketIdMapper<TId> mapper) where TId : unmanaged
        {
            mapper.Register<BooleanSyncPacket>();
            mapper.Register<CharSyncPacket>();
            mapper.Register<DoubleSyncPacket>();
            mapper.Register<Int16SyncPacket>();
            mapper.Register<Int32SyncPacket>();
            mapper.Register<Int64SyncPacket>();
            mapper.Register<SingleSyncPacket>();
            mapper.Register<StringSyncPacket>();
            mapper.Register<UInt16SyncPacket>();
            mapper.Register<UInt32SyncPacket>();
            mapper.Register<UInt64SyncPacket>();
        }
        
        public static void RegisterSyncPacketCodecs<TId>(this IPacketCodec<TId> codec, bool configPacketIdMapper = true) where TId : unmanaged
        {
            if (configPacketIdMapper)
                codec.PacketIdMapper.RegisterSyncPacketPacketIds();

            codec.RegisterCustomSerializer(packet => packet.Serialize(), BooleanSyncPacket.Deserialize);
            codec.RegisterCustomSerializer(packet => packet.Serialize(), CharSyncPacket.Deserialize);
            codec.RegisterCustomSerializer(packet => packet.Serialize(), DoubleSyncPacket.Deserialize);
            codec.RegisterCustomSerializer(packet => packet.Serialize(), Int16SyncPacket.Deserialize);
            codec.RegisterCustomSerializer(packet => packet.Serialize(), Int32SyncPacket.Deserialize);
            codec.RegisterCustomSerializer(packet => packet.Serialize(), Int64SyncPacket.Deserialize);
            codec.RegisterCustomSerializer(packet => packet.Serialize(), SingleSyncPacket.Deserialize);
            codec.RegisterCustomSerializer(packet => packet.Serialize(), StringSyncPacket.Deserialize);
            codec.RegisterCustomSerializer(packet => packet.Serialize(), UInt16SyncPacket.Deserialize);
            codec.RegisterCustomSerializer(packet => packet.Serialize(), UInt32SyncPacket.Deserialize);
            codec.RegisterCustomSerializer(packet => packet.Serialize(), UInt64SyncPacket.Deserialize);
        }

        public static void RegisterSyncPacketRoutes<TSession>(this IDataDispatcher<TSession> dispatcher) where TSession : ISession<TSession>
        {
            dispatcher.AddCustomPacketRoute(packet =>
            {
                if (packet.Payload == null) return null;

                var type = packet.Payload.GetType();

                return type switch
                {
                    _ when type == typeof(BooleanSyncPacket) => typeof(ISyncPacket),
                    _ when type == typeof(CharSyncPacket) => typeof(ISyncPacket),
                    _ when type == typeof(DoubleSyncPacket) => typeof(ISyncPacket),
                    _ when type == typeof(Int16SyncPacket) => typeof(ISyncPacket),
                    _ when type == typeof(Int32SyncPacket) => typeof(ISyncPacket),
                    _ when type == typeof(Int64SyncPacket) => typeof(ISyncPacket),
                    _ when type == typeof(SingleSyncPacket) => typeof(ISyncPacket),
                    _ when type == typeof(StringSyncPacket) => typeof(ISyncPacket),
                    _ when type == typeof(UInt16SyncPacket) => typeof(ISyncPacket),
                    _ when type == typeof(UInt32SyncPacket) => typeof(ISyncPacket),
                    _ when type == typeof(UInt64SyncPacket) => typeof(ISyncPacket),
                    _ => null
                };
            });
        }
    }
}