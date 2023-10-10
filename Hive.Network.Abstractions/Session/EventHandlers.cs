using System;

namespace Hive.Network.Abstractions.Session;

public delegate void SessionReceivedHandler(ISession session, ReadOnlyMemory<byte> rawMessage);