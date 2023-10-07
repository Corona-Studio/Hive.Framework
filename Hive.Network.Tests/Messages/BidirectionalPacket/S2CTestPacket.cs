using MemoryPack;
using ProtoBuf;

namespace Hive.Network.Tests.Messages.BidirectionalPacket;

[ProtoContract]
[MemoryPackable]
public partial class S2CTestPacket
{
    [ProtoMember(1)] public int ReversedRandomNumber { get; set; }
}