using MemoryPack;
using ProtoBuf;

namespace Hive.Server.Default.Messages;

[ProtoContract]
[MemoryPackable]
public partial class SigninMessage
{
    [ProtoMember(1)]
    public int Id { get; set; }
}