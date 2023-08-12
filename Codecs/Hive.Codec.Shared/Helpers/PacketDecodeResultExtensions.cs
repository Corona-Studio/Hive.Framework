using Hive.Framework.Codec.Abstractions;

namespace Hive.Codec.Shared.Helpers
{
    public static class PacketDecodeResultExtensions
    {
        public static PacketDecodeResult<object?> AsPacketDecodeResult<TId>(
            this PacketDecodeResultWithId<TId> decodeResultWithId)
            where TId : unmanaged
        {
            return new PacketDecodeResult<object?>(
                decodeResultWithId.Prefixes,
                decodeResultWithId.Flags,
                decodeResultWithId.Payload);
        }

        public static PacketDecodeResult<TPayload?> AsTypedPacketDecodeResult<TPayload>(
            this PacketDecodeResult<object> decodeResultWithId)
            where TPayload : class
        {
            return new PacketDecodeResult<TPayload?>(
                decodeResultWithId.Prefixes,
                decodeResultWithId.Flags,
                (TPayload?)decodeResultWithId.Payload);
        }
    }
}