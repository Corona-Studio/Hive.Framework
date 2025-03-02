using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Hive.Codec.Shared
{
    public class ReadOnlySequenceStream : Stream
    {
        private static readonly Task<int> TaskOfZero = Task.FromResult(0);

        /// <summary>
        /// A reusable task if two consecutive reads return the same number of bytes.
        /// </summary>
        private Task<int>? _lastReadTask;

        private readonly ReadOnlySequence<byte> _readOnlySequence;

        private SequencePosition _position;

        public ReadOnlySequenceStream(ReadOnlySequence<byte> readOnlySequence)
        {
            _readOnlySequence = readOnlySequence;
            _position = readOnlySequence.Start;
        }

        /// <inheritdoc/>
        public override bool CanRead => !IsDisposed;

        /// <inheritdoc/>
        public override bool CanSeek => !IsDisposed;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => ReturnOrThrowDisposed(_readOnlySequence.Length);

        /// <inheritdoc/>
        public override long Position
        {
            get => _readOnlySequence.Slice(0, _position).Length;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _position = _readOnlySequence.GetPosition(value, _readOnlySequence.Start);
            }
        }

        /// <inheritdoc/>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public override void Flush() => ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken) => throw ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadOnlySequence<byte> remaining = _readOnlySequence.Slice(_position);
            ReadOnlySequence<byte> toCopy = remaining.Slice(0, Math.Min(count, remaining.Length));
            _position = toCopy.End;
            toCopy.CopyTo(buffer.AsSpan(offset, count));
            return (int)toCopy.Length;
        }

        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int bytesRead = Read(buffer, offset, count);
            if (bytesRead == 0)
            {
                return TaskOfZero;
            }

#pragma warning disable VSTHRD103 // Call async methods when in an async method - This task is guaranteed to already be complete.
            if (_lastReadTask?.Result == bytesRead)
            {
                return _lastReadTask;
            }
            else
            {
                return _lastReadTask = Task.FromResult(bytesRead);
            }
#pragma warning restore VSTHRD103 // Call async methods when in an async method
        }

        /// <inheritdoc/>
        public override int ReadByte()
        {
            ReadOnlySequence<byte> remaining = _readOnlySequence.Slice(_position);
            if (remaining.Length > 0)
            {
                byte result = remaining.First.Span[0];
                _position = _readOnlySequence.GetPosition(1, _position);
                return result;
            }
            else
            {
                return -1;
            }
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(ReadOnlySequenceStream));

            SequencePosition relativeTo;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    relativeTo = _readOnlySequence.Start;
                    break;
                case SeekOrigin.Current:
                    if (offset >= 0)
                    {
                        relativeTo = _position;
                    }
                    else
                    {
                        relativeTo = _readOnlySequence.Start;
                        offset += Position;
                    }

                    break;
                case SeekOrigin.End:
                    if (offset >= 0)
                    {
                        relativeTo = _readOnlySequence.End;
                    }
                    else
                    {
                        relativeTo = _readOnlySequence.Start;
                        offset += Length;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }

            _position = _readOnlySequence.GetPosition(offset, relativeTo);
            return Position;
        }

        /// <inheritdoc/>
        public override void SetLength(long value) => ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc/>
        public override void WriteByte(byte value) => ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc/>
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            foreach (ReadOnlyMemory<byte> segment in _readOnlySequence)
            {
                await destination.WriteAsync(segment, cancellationToken).ConfigureAwait(false);
            }
        }

#if SPAN_BUILTIN

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer)
        {
            ReadOnlySequence<byte> remaining = this.readOnlySequence.Slice(this.position);
            ReadOnlySequence<byte> toCopy = remaining.Slice(0, Math.Min(buffer.Length, remaining.Length));
            this.position = toCopy.End;
            toCopy.CopyTo(buffer);
            return (int)toCopy.Length;
        }

        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<int>(this.Read(buffer.Span));
        }

        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer) => throw this.ThrowDisposedOr(new NotSupportedException());

        /// <inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw this.ThrowDisposedOr(new NotSupportedException());

#endif

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }

        private T ReturnOrThrowDisposed<T>(T value)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(ReadOnlySequenceStream));

            return value;
        }

        private Exception ThrowDisposedOr(Exception ex)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(ReadOnlySequenceStream));

            throw ex;
        }
    }
}