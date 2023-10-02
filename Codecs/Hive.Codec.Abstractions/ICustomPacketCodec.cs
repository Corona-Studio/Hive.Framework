using System;
using System.Buffers;
using System.IO;
using Hive.Framework.Shared;

namespace Hive.Framework.Codec.Abstractions
{
    /// <summary>
    /// 自定义封包编解码器接口
    /// </summary>
    public interface ICustomPacketCodec
    {
        int EncodeBody<T>(T message, Stream stream);

        object DecodeBody(Stream stream, Type type);
    }
}