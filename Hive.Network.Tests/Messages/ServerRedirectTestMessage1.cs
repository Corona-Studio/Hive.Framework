using MemoryPack;
using ProtoBuf;

namespace Hive.Network.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ServerRedirectTestMessage1
{
    [ProtoMember(1)] public string? Content { get; set; }
}