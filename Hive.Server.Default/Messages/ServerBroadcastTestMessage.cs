using MemoryPack;
using ProtoBuf;

namespace Hive.Server.Default.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ServerBroadcastTestMessage
{
    [ProtoMember(1)]
    public int Number { get; set; }
}