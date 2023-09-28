using Hive.Framework.Networking.Abstractions;
using Microsoft.IO;

namespace Hive.Framework.Networking.Shared;

public class RecyclableMessageStreamPool : IMessageStreamPool
{
    private static RecyclableMemoryStreamManager Manager { get; } = new();

    public void Free(IMessageStream buffer)
    {
        if(buffer is RecyclableMessageStream rms)
            rms.Dispose();
    }

    public IMessageStream Alloc(string? tag)
    {
        var stream = (RecyclableMemoryStream)Manager.GetStream(tag);
        return new RecyclableMessageStream(stream);
    }
}