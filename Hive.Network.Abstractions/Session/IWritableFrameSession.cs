using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Network.Abstractions.Session;

/// <summary>Optional fast path for a caller-owned contiguous frame buffer.</summary>
public interface IWritableFrameSession : IBorrowedBufferSession
{
    /// <summary>
    /// The first six bytes are reserved for the Hive header; payload starts at
    /// offset six. The session writes the header and sends the entire frame in
    /// one transport operation unless a partial send requires more. The caller
    /// must own the buffer and keep it unchanged until this ValueTask completes.
    /// This avoids a payload copy and a separate header send. The existing wire
    /// format, length limit and serialization with other sends are preserved.
    /// </summary>
    ValueTask<bool> TrySendFrameAsync(Memory<byte> frame, CancellationToken token = default);
}
