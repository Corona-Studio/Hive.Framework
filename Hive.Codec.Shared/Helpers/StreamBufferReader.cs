using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace Hive.Codec.Shared.Helpers;

public struct StreamBufferReader : IDisposable, IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly byte[] _buffer;
    private int _position;
    private int Remaining => _buffer.Length - _position;

    public StreamBufferReader(Stream stream)
    {
        _stream = stream;
        _buffer = ArrayPool<byte>.Shared.Rent((int)_stream.Length);
        _position = 0;
    }

    public ReadOnlySpan<byte> Read(int len = 0)
    {
        if (len == 0) len = Remaining;
        var read = _stream.Read(_buffer, _position, Math.Min(Remaining, len));
        _position += read;
        return _buffer.AsSpan(_position - read, read);
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return new ValueTask();
    }
}