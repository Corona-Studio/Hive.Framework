using MemoryPack;
using ProtoBuf;

namespace Hive.Server.Default.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ServerRegistrationMessage
{
    [ProtoMember(1)]
    public ushort[] PackagesToReceive { get; set; }
}