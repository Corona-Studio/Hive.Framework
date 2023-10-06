using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace Hive.Codec.Shared.Helpers
{
    public struct StreamBufferWriter : IBufferWriter<byte>, IDisposable, IAsyncDisposable
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer;
        private int _position;
        public int WrittenCount { get; private set; }
        public StreamBufferWriter(Stream stream, int bufferSize = 4096)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            _position = 0;
            WrittenCount = 0;
        }

        public void Advance(int count)
        {
            if (_position + count > _buffer.Length)
            {
                Flush();
            }
            _position += count;
            WrittenCount += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            if (_position + sizeHint > _buffer.Length)
            {
                Flush();
            }
            return _buffer.AsMemory(_position);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            if (_position + sizeHint > _buffer.Length)
            {
                Flush();
            }
            return _buffer.AsSpan(_position);
        }

        public void Flush()
        {
            _stream.Write(_buffer, 0, _position);
            _position = 0;
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        public ValueTask DisposeAsync()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            return new ValueTask();
        }
    }
}