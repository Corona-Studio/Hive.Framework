using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Network.Abstractions.Session;

/// <summary>A stream session that supports processing and sending borrowed buffers.</summary>
public interface IBorrowedBufferSession : ISession
{
    /// <summary>
    /// A single asynchronous consumer. The receive buffer remains valid until its
    /// ValueTask completes. Await all uses of the buffer before returning; do not
    /// retain it in a queue or start fire-and-forget work. Applies backpressure.
    /// </summary>
    SessionReceivedAsyncHandler? ReceiveHandler { get; set; }

    /// <summary>
    /// Sends an entire framed packet directly from the sequence. The caller owns
    /// the buffer and must keep it valid until completion. No payload copy is made.
    /// Concurrent sends are serialized; a failed partial frame closes the session.
    /// </summary>
    ValueTask<bool> TrySendAsync(ReadOnlySequence<byte> payload, CancellationToken token = default);
}

public delegate ValueTask SessionReceivedAsyncHandler(
    ISession session, ReadOnlySequence<byte> buffer, CancellationToken token);
