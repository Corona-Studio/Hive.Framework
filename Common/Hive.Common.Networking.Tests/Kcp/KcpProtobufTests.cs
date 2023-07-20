using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Kcp;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Shared;
using System.Net;

namespace Hive.Framework.Networking.Tests.Kcp;

[TestFixture]
public class KcpProtobufTests : KcpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new ProtoBufPacketIdMapper();
        RegisterMessages();

        Codec = new ProtoBufPacketCodec(PacketIdMapper);
        ClientManager = new FakeKcpClientManager();
        DataDispatcher = new DefaultDataDispatcher<KcpSession<ushort>>();

        Server = new KcpAcceptor<ushort, Guid>(_endPoint, Codec, DataDispatcher, ClientManager);
        Server.Start();

        Client = new KcpSession<ushort>(_endPoint, Codec, DataDispatcher);
    }
}