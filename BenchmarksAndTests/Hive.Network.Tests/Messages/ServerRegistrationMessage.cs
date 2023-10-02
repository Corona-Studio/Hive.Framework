using MemoryPack;
using ProtoBuf;

namespace Hive.Network.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ServerRegistrationMessage
{
    [ProtoMember(1)]
    public ushort[] PackagesToReceive { get; set; }
}