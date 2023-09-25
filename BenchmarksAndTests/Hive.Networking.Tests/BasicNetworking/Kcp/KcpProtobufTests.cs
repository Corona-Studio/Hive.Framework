using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Kcp;

[TestFixture]
public sealed class KcpProtobufTests : KcpTestBase
{
    public override IPacketCodec<ushort> CreateCodec(IPacketIdMapper<ushort> mapper)
    {
        return new ProtoBufPacketCodec(mapper);
    }
}