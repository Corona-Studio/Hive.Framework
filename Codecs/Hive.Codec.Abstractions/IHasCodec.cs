namespace Hive.Framework.Codec.Abstractions
{
    /// <summary>
    /// 编解码器抽象
    /// </summary>
    /// <typeparam name="TId">封包 ID 类型（通常为 ushort）</typeparam>
    public interface IHasCodec<TId> where TId : unmanaged
    {
        IPacketCodec<TId> Codec { get; }
    }
}