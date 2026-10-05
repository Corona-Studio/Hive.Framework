using System;
using System.Buffers;
using System.IO;
using System.Threading;

namespace Hive.Common.Shared.Pooling
{
    /// <summary>
    /// Reusable stream/buffer writer with shared ownership. Rent holds one reference;
    /// Retain adds a reference and Dispose releases it. Each reference must be released
    /// once. Never access a stream after releasing the last reference.
    /// </summary>
    public sealed class PooledBufferStream : Stream, IBufferWriter<byte>, IMemoryOwner<byte>
    {
        private const int RetainedCapacity = 128 * 1024;
        private static readonly BoundedObjectPool<PooledBufferStream> Pool = new(() => new PooledBufferStream());
        private byte[] _buffer = Array.Empty<byte>();
        private int _length;
        private int _position;
        private int _references;
        private int _limit;

        private PooledBufferStream() { }

        public static PooledBufferStream Rent(int limit = ushort.MaxValue)
        {
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            var stream = Pool.Rent();
            stream._length = stream._position = 0;
            stream._limit = limit;
            stream._references = 1;
            return stream;
        }

        public PooledBufferStream Retain()
        {
            while (true)
            {
                var references = Volatile.Read(ref _references);
                if (references <= 0) throw new ObjectDisposedException(nameof(PooledBufferStream));
                if (Interlocked.CompareExchange(ref _references, references + 1, references) == references) return this;
            }
        }

        public Memory<byte> Memory => _buffer.AsMemory(0, _length);
        public ReadOnlySequence<byte> WrittenSequence => new(Memory);
        public override bool CanRead => _references > 0;
        public override bool CanSeek => _references > 0;
        public override bool CanWrite => _references > 0;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _length) throw new ArgumentOutOfRangeException(nameof(value));
                _position = (int)value;
            }
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(_position, Math.Min(_buffer.Length, _limit) - _position);
        }

        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

        public void Advance(int count)
        {
            if (count < 0 || count > Math.Min(_buffer.Length, _limit) - _position)
                throw new ArgumentOutOfRangeException(nameof(count));
            _position += count;
            if (_position > _length) _length = _position;
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (_references <= 0) throw new ObjectDisposedException(nameof(PooledBufferStream));
            if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
            var required = (long)_position + Math.Max(1, sizeHint);
            if (required > _limit) throw new InvalidDataException("Packet exceeds the configured buffer limit.");
            if (required <= _buffer.Length) return;
            var capacity = (int)Math.Min(_limit, Math.Max(required, Math.Max(256L, (long)_buffer.Length * 2)));
            var replacement = ArrayPool<byte>.Shared.Rent(capacity);
            _buffer.AsSpan(0, _length).CopyTo(replacement);
            var previous = _buffer;
            _buffer = replacement;
            if (previous.Length != 0) ArrayPool<byte>.Shared.Return(previous);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty) return;
            buffer.CopyTo(GetSpan(buffer.Length));
            Advance(buffer.Length);
        }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void WriteByte(byte value) { GetSpan(1)[0] = value; Advance(1); }
        public override int Read(Span<byte> buffer)
        {
            var count = Math.Min(buffer.Length, _length - _position);
            _buffer.AsSpan(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            return _position;
        }
        public override void SetLength(long value)
        {
            if (value < 0 || value > _limit) throw new ArgumentOutOfRangeException(nameof(value));
            if (value > _length)
            {
                var position = _position;
                _position = _length;
                GetSpan((int)value - _length).Slice(0, (int)value - _length).Clear();
                _position = position;
            }
            _length = (int)value;
            if (_position > _length) _position = _length;
        }
        public override void Flush() { }

        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;
            var remaining = Interlocked.Decrement(ref _references);
            if (remaining > 0) return;
            if (remaining < 0) throw new ObjectDisposedException(nameof(PooledBufferStream), "A buffer reference was released twice.");
            _length = _position = 0;
            // Large exceptional packets must not permanently inflate the object cache.
            if (_buffer.Length > RetainedCapacity || !Pool.TryReturn(this))
            {
                var buffer = _buffer;
                _buffer = Array.Empty<byte>();
                if (buffer.Length != 0) ArrayPool<byte>.Shared.Return(buffer);
                if (buffer.Length > RetainedCapacity) Pool.TryReturn(this);
            }
        }
    }
}
