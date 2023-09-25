using MemoryPack;
using ProtoBuf;

namespace Hive.Server.Default.Messages;

[ProtoContract]
[MemoryPackable]
public partial class CountTestMessage
{
    [ProtoMember(1)]
    public int Adder { get; set; }
}