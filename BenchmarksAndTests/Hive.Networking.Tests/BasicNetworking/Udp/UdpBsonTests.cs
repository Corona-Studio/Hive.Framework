using Hive.Framework.Codec.Bson;
using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Udp;

[TestFixture]
public sealed class UdpBsonTests : UdpTestBase
{
    public override IPacketCodec<ushort> CreateCodec(IPacketIdMapper<ushort> mapper)
    {
        return new BsonPacketCodec(mapper);
    }
}