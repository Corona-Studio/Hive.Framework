namespace Hive.Codec.Abstractions
{
    public interface ICustomCodecProvider
    {
        ICustomPacketCodec? GetPacketCodec(PacketId id);
    }
}