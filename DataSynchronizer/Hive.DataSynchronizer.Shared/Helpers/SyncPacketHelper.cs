using Hive.DataSynchronizer.Abstraction.Interfaces;
using Hive.DataSynchronizer.Shared.ObjectSyncPacket;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;

namespace Hive.DataSynchronizer.Shared.Helpers
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
                    var t when t == typeof(BooleanSyncPacket) => typeof(ISyncPacket),
                    var t when t == typeof(CharSyncPacket) => typeof(ISyncPacket),
                    var t when t == typeof(DoubleSyncPacket) => typeof(ISyncPacket),
                    var t when t == typeof(Int16SyncPacket) => typeof(ISyncPacket),
                    var t when t == typeof(Int32SyncPacket) => typeof(ISyncPacket),
                    var t when t == typeof(Int64SyncPacket) => typeof(ISyncPacket),
                    var t when t == typeof(SingleSyncPacket) => typeof(ISyncPacket),
                    var t when t == typeof(StringSyncPacket) => typeof(ISyncPacket),
                    var t when t == typeof(UInt16SyncPacket) => typeof(ISyncPacket),
                    var t when t == typeof(UInt32SyncPacket) => typeof(ISyncPacket),
                    var t when t == typeof(UInt64SyncPacket) => typeof(ISyncPacket),
                    _ => null
                };
            });
        }
    }
}