using Hive.Framework.Codec.Bson;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Udp;
using Hive.Framework.Shared;
using System.Net;

namespace Hive.Framework.Networking.Tests.Udp;

[TestFixture]
public sealed class UdpBsonTests : UdpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new BsonPacketIdMapper();
        RegisterMessages();

        Codec = new BsonPacketCodec(PacketIdMapper);
        ClientManager = new FakeUdpClientManager();
        DataDispatcher = new DefaultDataDispatcher<UdpSession<ushort>>();

        Server = new UdpAcceptor<ushort, Guid>(_endPoint, Codec, DataDispatcher, ClientManager);
        Server.Start();

        Client = new UdpSession<ushort>(_endPoint, Codec, DataDispatcher);
    }
}