namespace Hive.Framework.Codec.Abstractions
{
    public interface IPacketDecodeResult<out TPayload>
    {
        object?[] Prefixes { get; }
        TPayload Payload { get; }
    }

    public readonly struct PacketDecodeResultWithId<TId> : IPacketDecodeResult<object> where TId : struct
    {
        public object?[] Prefixes { get; }
        public object Payload { get; }
        public TId PacketId { get; }

        public PacketDecodeResultWithId(object?[] prefixes, TId packetId, object payload)
        {
            Prefixes = prefixes;
            PacketId = packetId;
            Payload = payload;
        }
    }

    public readonly struct PacketDecodeResult<TPayload> : IPacketDecodeResult<TPayload>
    {
        public object?[] Prefixes { get; }
        public TPayload Payload { get; }

        public PacketDecodeResult(object?[] prefixes, TPayload payload)
        {
            Prefixes = prefixes;
            Payload = payload;
        }
    }
}