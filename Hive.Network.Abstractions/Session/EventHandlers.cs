using System.Buffers;

namespace Hive.Network.Abstractions.Session;

public delegate void SessionReceivedHandler(ISession session, ReadOnlySequence<byte> buffer);