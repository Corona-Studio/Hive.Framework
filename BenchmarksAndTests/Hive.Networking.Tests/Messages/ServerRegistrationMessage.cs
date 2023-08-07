using MemoryPack;
using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ServerRegistrationMessage
{
    [ProtoMember(1)]
    public ushort[] PackagesToReceive { get; set; }
}