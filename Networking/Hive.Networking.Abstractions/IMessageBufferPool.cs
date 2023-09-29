using System.Buffers;
using System.IO;

namespace Hive.Framework.Networking.Abstractions;

public interface IMessageBufferPool
{
    IMessageBuffer Rent(string? tag=null);
    void Free(IMessageBuffer buffer);
}