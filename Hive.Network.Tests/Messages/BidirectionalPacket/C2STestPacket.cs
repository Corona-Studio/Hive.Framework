using MemoryPack;
using ProtoBuf;

namespace Hive.Network.Tests.Messages.BidirectionalPacket;

[ProtoContract]
[MemoryPackable]
public partial class C2STestPacket
{
    [ProtoMember(1)] public int RandomNumber { get; set; }
}