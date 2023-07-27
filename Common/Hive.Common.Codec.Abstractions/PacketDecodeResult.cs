namespace Hive.Framework.Codec.Abstractions
{
    public class PacketDecodeResult<TId> where TId : struct
    {
        public object?[] Prefixes { get; }
        public TId PacketId { get; }
        public object Payload { get; }

        public PacketDecodeResult(object?[] prefixes, TId packetId, object payload)
        {
            Prefixes = prefixes;
            PacketId = packetId;
            Payload = payload;
        }
    }
}