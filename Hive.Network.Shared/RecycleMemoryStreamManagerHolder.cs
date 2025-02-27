using Microsoft.IO;

namespace Hive.Network.Shared
{
    public static class RecycleMemoryStreamManagerHolder
    {
        public static RecyclableMemoryStreamManager Shared { get; } = new();
    }
}