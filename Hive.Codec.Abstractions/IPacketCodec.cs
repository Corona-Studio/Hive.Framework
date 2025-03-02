using System.Buffers;
using System.IO;

namespace Hive.Codec.Abstractions
{
    /// <summary>
    ///     封包编解码器接口
    /// </summary>
    public interface IPacketCodec
    {
        int Encode<T>(T message, Stream stream);
        object? Decode(ReadOnlySequence<byte> buffer);
    }
}