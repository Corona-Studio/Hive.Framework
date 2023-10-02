namespace Hive.Framework.Codec.Abstractions
{
    public interface ICustomCodecProvider
    {
        ICustomPacketCodec? GetPacketCodec(PacketId id);
    }
}