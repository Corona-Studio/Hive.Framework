using System.Buffers;

namespace Hive.Network.Abstractions.Session;

public delegate void SessionReceivedHandler(ISession session, ReadOnlySequence<byte> buffer);
public delegate void SessionRawReceivedHandler(ISession session, ReadOnlySequence<byte> buffer);