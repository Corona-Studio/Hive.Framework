using System.Buffers;
using Microsoft.Extensions.ObjectPool;

namespace Hive.Framework.Shared
{
    public class BufferWriterPoolPolicy : IPooledObjectPolicy<ArrayBufferWriter<byte>>
    {
        public ArrayBufferWriter<byte> Create() => new (1024);

        public bool Return(ArrayBufferWriter<byte> obj)
        {
            obj.Clear();

            return true;
        }
    }
}