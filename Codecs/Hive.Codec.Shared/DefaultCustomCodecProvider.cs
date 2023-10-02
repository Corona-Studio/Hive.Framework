using Hive.Framework.Codec.Abstractions;

namespace Hive.Codec.Shared
{
    public class DefaultCustomCodecProvider: ICustomCodecProvider
    {
        public ICustomPacketCodec? GetPacketCodec(PacketId id)
        {
            return null;
        }
    }
}