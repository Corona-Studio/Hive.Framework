using System;
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
        T Decode<T>(Stream stream);
    }
}