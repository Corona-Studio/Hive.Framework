using System.Buffers;
using System.IO;

namespace Hive.Framework.Networking.Abstractions;

public interface IMessageStreamPool
{
    IMessageStream Alloc(string? tag=null);
    void Free(IMessageStream buffer);
}