using Hive.Network.Abstractions;
using Microsoft.IO;

namespace Hive.Network.Shared
{
    public class RecyclableMessageBufferPool : IMessageBufferPool
    {
        private static RecyclableMemoryStreamManager Manager => RecycleMemoryStreamManagerHolder.Shared;

        public void Free(IMessageBuffer buffer)
        {
            if(buffer is RecyclableMessageBuffer rms)
                rms.Free();
        }

        public IMessageBuffer Rent(string? tag)
        {
            var stream = (RecyclableMemoryStream)Manager.GetStream(tag);
            return new RecyclableMessageBuffer(stream);
        }
    }
}