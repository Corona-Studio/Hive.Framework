using System.Buffers;

namespace Hive.Framework.Codec.Abstractions
{
    /// <summary>
    /// 封包生成接口，用于打包编码后的数据
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public interface IPacketGenerator<TData>
    {
        IPacketIdMapper<TData> PacketIdMapper { get; }

        void Generate<T>(T obj, IBufferWriter<TData> writer) where T : unmanaged;
    }
}