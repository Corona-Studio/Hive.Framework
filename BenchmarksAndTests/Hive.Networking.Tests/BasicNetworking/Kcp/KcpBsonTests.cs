using Hive.Framework.Codec.Bson;
using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Kcp;

[TestFixture]
public sealed class KcpBsonTests : KcpTestBase
{
    public override IPacketCodec<ushort> CreateCodec(IPacketIdMapper<ushort> mapper)
    {
        return new BsonPacketCodec(mapper);
    }
}