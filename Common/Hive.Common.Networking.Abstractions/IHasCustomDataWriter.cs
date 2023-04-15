using System.Buffers;

namespace Hive.Framework.Networking.Abstractions;

public interface IHasCustomDataWriter
{
    IBufferWriter<byte> DataWriter { get; }
}