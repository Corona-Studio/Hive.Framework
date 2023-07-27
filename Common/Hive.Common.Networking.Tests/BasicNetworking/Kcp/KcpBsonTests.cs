using Hive.Framework.Codec.Bson;
using Hive.Framework.Networking.Kcp;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Shared;
using System.Net;
using Hive.Common.Codec.Shared;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Kcp;

[TestFixture]
public sealed class KcpBsonTests : KcpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new DefaultPacketIdMapper();
        RegisterMessages();

        Codec = new BsonPacketCodec(PacketIdMapper);
        ClientManager = new FakeKcpClientManager();
        DataDispatcherProvider = () => new DefaultDataDispatcher<KcpSession<ushort>>();

        Server = new KcpAcceptor<ushort, Guid>(_endPoint, Codec, DataDispatcherProvider, ClientManager);
        Server.Start();

        Client = new KcpSession<ushort>(_endPoint, Codec, DataDispatcherProvider());
    }
}