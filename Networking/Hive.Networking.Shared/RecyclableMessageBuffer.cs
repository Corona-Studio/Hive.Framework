using System;
using System.IO;
using System.Threading.Tasks;
using Hive.Framework.Networking.Abstractions;
using Microsoft.IO;

namespace Hive.Framework.Networking.Shared;

public class RecyclableMessageBuffer : IMessageBuffer, IDisposable, IAsyncDisposable
{
    internal readonly RecyclableMemoryStream Stream;
    int _offset = 0;
    int _length = 0;
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

    public void SetSlice(int offset, int length)
    {
        _offset = offset;
        _length = length;
    }

    public Memory<byte> GetFinalBufferMemory()
    {
        return Stream.GetBuffer().AsMemory(_offset,_length);
    }

    public ArraySegment<byte> GetArraySegment()
    {
        return new ArraySegment<byte>(Stream.GetBuffer(),_offset,_length);
    }

    public int Length => (int)Stream.Length;

    public void Dispose()
    {
        Stream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync();
    }
}