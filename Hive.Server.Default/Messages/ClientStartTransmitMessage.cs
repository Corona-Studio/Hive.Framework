using MemoryPack;
using ProtoBuf;

namespace Hive.Server.Default.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ClientStartTransmitMessage
{
    [ProtoMember(1)]
    public ushort[] RedirectPacketIds { get; set; }
}