using System;
using System.Buffers;
using System.IO;

namespace Hive.Codec.Abstractions
{
    /// <summary>
    ///     自定义封包编解码器接口
    /// </summary>
    public interface ICustomPacketCodec
    {
        int EncodeBody<T>(T message, Stream stream);

        object DecodeBody(ReadOnlySequence<byte> buffer, Type type);
    }
}