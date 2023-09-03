using Hive.Framework.Shared;

namespace Hive.DataSynchronizer.Shared.Helpers
{
    public static class SyncOptionsHelper
    {
        public static PacketFlags ToPacketFlags(this SyncOptions syncOptions)
        {
            return syncOptions switch
            {
                SyncOptions.ClientOnly => PacketFlags.Broadcast | PacketFlags.S2CPacket,
                SyncOptions.ServerOnly => PacketFlags.Broadcast | PacketFlags.C2SPacket,
                SyncOptions.AllSession => PacketFlags.Broadcast | PacketFlags.C2SPacket | PacketFlags.S2CPacket,
                _ => PacketFlags.None,
            };
        }
    }
}
