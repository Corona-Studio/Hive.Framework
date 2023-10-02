using System;
using System.Buffers;
using System.IO;
using Hive.Framework.Shared;

namespace Hive.Framework.Codec.Abstractions
{
    /// <summary>
    /// 封包编解码器接口
    /// </summary>
    public interface IPacketCodec
    {
        int Encode<T>(T message, Stream stream);
        object? Decode(Stream stream);
    }
}