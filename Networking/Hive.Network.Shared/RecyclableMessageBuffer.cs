using System;
using System.Threading.Tasks;
using Hive.Network.Abstractions;
using Microsoft.IO;

namespace Hive.Network.Shared
{
    public class RecyclableMessageBuffer : IMessageBuffer
    {
        internal readonly RecyclableMemoryStream Stream;
        public RecyclableMessageBuffer(RecyclableMemoryStream stream)
        {
            Stream = stream;
        }

        public void Advance(int count)
        {
            Stream.Advance(count);
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            return Stream.GetMemory(sizeHint);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            return Stream.GetSpan(sizeHint);
        }

        public Memory<byte> GetFinalBufferMemory()
        {
            return Stream.GetBuffer().AsMemory(0,(int)Stream.Length);
        }

        public ArraySegment<byte> GetArraySegment()
        {
            return new ArraySegment<byte>(Stream.GetBuffer(),0,(int)Stream.Length);
        }

        public int Length => (int)Stream.Length;
        public void Free()
        {
            Stream.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await Stream.DisposeAsync();
        }
    }
}