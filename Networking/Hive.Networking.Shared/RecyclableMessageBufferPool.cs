using Hive.Framework.Networking.Abstractions;
using Microsoft.IO;

namespace Hive.Framework.Networking.Shared;

public class RecyclableMessageBufferPool : IMessageBufferPool
{
    private static RecyclableMemoryStreamManager Manager { get; } = new();

    public void Free(IMessageBuffer buffer)
    {
        if(buffer is RecyclableMessageBuffer rms)
            rms.Dispose();
    }

    public IMessageBuffer Rent(string? tag)
    {
        var stream = (RecyclableMemoryStream)Manager.GetStream(tag);
        return new RecyclableMessageBuffer(stream);
    }
}