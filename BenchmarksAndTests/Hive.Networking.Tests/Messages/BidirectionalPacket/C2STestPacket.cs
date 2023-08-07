using MemoryPack;
using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages.BidirectionalPacket;

[ProtoContract]
[MemoryPackable]
public partial class C2STestPacket
{
    [ProtoMember(1)]
    public int RandomNumber { get; set; }
}