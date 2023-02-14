using System.Buffers;

namespace Hive.Framework.Codec.Abstractions
{
    public interface IEncoder<TData>
    {
        IBufferWriter<TData> Encode<T>(T obj) where T : unmanaged;
    }
}