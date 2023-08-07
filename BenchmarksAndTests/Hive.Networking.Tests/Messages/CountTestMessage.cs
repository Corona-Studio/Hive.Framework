using MemoryPack;
using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class CountTestMessage
{
    [ProtoMember(1)]
    public int Adder { get; set; }
}