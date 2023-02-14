namespace Hive.Framework.Codec.Protobuf;

public readonly struct ProtoBufResolveInfo
{
    /// <summary>
    /// 封包长度
    /// </summary>
    public readonly ushort Length;

    /// <summary>
    /// 封包 ID
    /// </summary>
    public readonly byte PacketId;

    public ProtoBufResolveInfo(ushort length, byte packetId)
    {
        Length = length;
        PacketId = packetId;
    }
}