using Hive.Codec.MemoryPack;
using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Kcp;

[TestFixture]
public sealed class KcpMemoryPackTests : KcpTestBase
{
    public override IPacketCodec<ushort> CreateCodec(IPacketIdMapper<ushort> mapper)
    {
        return new MemoryPackPacketCodec(mapper);
    }
}