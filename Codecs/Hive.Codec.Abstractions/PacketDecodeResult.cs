using Hive.Framework.Shared;

namespace Hive.Framework.Codec.Abstractions
{
    public readonly struct PacketDecodeResultWithId<TId> : IPacketDecodeResult<object>
        where TId : unmanaged
    {
        public object?[]? Prefixes { get; }
        public PacketFlags Flags { get; }
        public object? Payload { get; }
        public TId PacketId { get; }

        public PacketDecodeResultWithId(
            PacketFlags flags,
            TId packetId)
        {
            Prefixes = null;
            Flags = flags;
            PacketId = packetId;
            Payload = null;
        }

        public PacketDecodeResultWithId(
            object?[] prefixes,
            PacketFlags flags,
            TId packetId)
        {
            Prefixes = prefixes;
            Flags = flags;
            PacketId = packetId;
            Payload = null;
        }

        public PacketDecodeResultWithId(
            object?[] prefixes,
            PacketFlags flags,
            TId packetId,
            object payload)
        {
            Prefixes = prefixes;
            Flags = flags;
            PacketId = packetId;
            Payload = payload;
        }
    }

    public readonly struct PacketDecodeResult<TPayload> : IPacketDecodeResult<TPayload>
    {
        public object?[]? Prefixes { get; }
        public PacketFlags Flags { get; }
        public TPayload Payload { get; }

        public PacketDecodeResult(
            object?[]? prefixes,
            PacketFlags flags,
            TPayload payload)
        {
            Prefixes = prefixes;
            Flags = flags;
            Payload = payload;
        }
    }
}