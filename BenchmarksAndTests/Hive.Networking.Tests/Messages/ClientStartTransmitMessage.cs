using MemoryPack;
using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ClientStartTransmitMessage
{
    [ProtoMember(1)]
    public ushort[] RedirectPacketIds { get; set; }
}