using Hive.Codec.MemoryPack;
using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Udp;

[TestFixture]
public sealed class UdpMemoryPackTests : UdpTestBase
{
    public override IPacketCodec<ushort> CreateCodec(IPacketIdMapper<ushort> mapper)
    {
        return new MemoryPackPacketCodec(mapper);
    }
}