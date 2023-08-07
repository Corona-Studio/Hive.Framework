using MemoryPack;
using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class SigninMessage
{
    [ProtoMember(1)]
    public int Id { get; set; }
}