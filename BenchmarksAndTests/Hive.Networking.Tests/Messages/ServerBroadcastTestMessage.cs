﻿using MemoryPack;
using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ServerBroadcastTestMessage
{
    [ProtoMember(1)]
    public int Number { get; set; }
}