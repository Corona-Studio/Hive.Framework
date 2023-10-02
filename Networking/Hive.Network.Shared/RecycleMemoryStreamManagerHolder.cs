using Microsoft.IO;

namespace Hive.Network.Shared
{
    public class RecycleMemoryStreamManagerHolder
    {
        public static RecyclableMemoryStreamManager Shared { get; } = new();
    }
}