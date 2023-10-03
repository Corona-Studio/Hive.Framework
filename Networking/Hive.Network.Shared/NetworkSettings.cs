namespace Hive.Network.Shared
{
    public static class NetworkSettings
    {
        public const int MaxMessageSize = 1024 * 1024 * 10;
        public const int DefaultBufferSize = 40960;
        public const int DefaultSocketBufferSize = 8192 * 4;
        public const int PacketHeaderLength = sizeof(ushort) + sizeof(uint); // 包头长度2Byte
    
        public const int PacketLengthOffset = 0;
        public const int SessionIdOffset = 2;
        public const int PacketBodyOffset = 6;

        public const int HandshakeSessionId = 0x114514;
    
        public const int MaxHeartBeatTimeout = 10000;
    }
}