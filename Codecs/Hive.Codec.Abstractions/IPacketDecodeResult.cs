using Hive.Framework.Shared;

namespace Hive.Framework.Codec.Abstractions
{
    public interface IPacketDecodeResult<out TPayload>
    {
        object?[]? Prefixes { get; }
        PacketFlags Flags { get; }
        TPayload Payload { get; }
    }
}