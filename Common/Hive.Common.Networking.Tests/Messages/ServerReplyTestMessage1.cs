﻿using MemoryPack;
using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ServerReplyTestMessage1
{
    [ProtoMember(1)]
    public string? Content { get; set; }
}