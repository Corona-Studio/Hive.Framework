using System;
using System.Threading.Tasks;
using Hive.Framework.Networking.Abstractions;
using Microsoft.IO;

namespace Hive.Framework.Networking.Shared;

public class RecyclableMessageStream : IMessageStream, IDisposable, IAsyncDisposable
{
    internal readonly RecyclableMemoryStream Stream;

    public RecyclableMessageStream(RecyclableMemoryStream stream)
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

    public ReadOnlyMemory<byte> GetBufferMemory()
    {
        return Stream.GetBuffer().AsMemory(0,(int)Stream.Length);
    }

    public ArraySegment<byte> GetArraySegment()
    {
        Stream.GetBuffer()
        return new ArraySegment<byte>(Stream.GetBuffer(),0,(int)Stream.Length);
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