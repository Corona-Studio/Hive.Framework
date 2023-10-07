using MemoryPack;
using ProtoBuf;

namespace Hive.Server.Default.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ServerRedirectTestMessage2
{
    [ProtoMember(1)] public int Value { get; set; }
}