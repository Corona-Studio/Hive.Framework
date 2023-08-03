﻿using MemoryPack;
using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ServerRedirectTestMessage2
{
    [ProtoMember(1)]
    public int Value { get; set; }
}