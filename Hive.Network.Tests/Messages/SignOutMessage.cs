using MemoryPack;
using ProtoBuf;

namespace Hive.Network.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class SignOutMessage
{
    [ProtoMember(1)]
    public int Id { get; set; }
}