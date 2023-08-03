using Hive.Framework.Networking.Abstractions;
using MemoryPack;
using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class DefaultServerReplyPacket : IServerReplyPacket<Guid>
{
    [ProtoMember(1)]
    public Guid SendTo { get; set; }

    [ProtoMember(2)]
    public ReadOnlyMemory<byte> InnerPayload { get; set; }
}