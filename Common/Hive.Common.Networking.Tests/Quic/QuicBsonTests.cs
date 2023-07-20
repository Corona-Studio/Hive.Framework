using Hive.Framework.Codec.Bson;
using Hive.Framework.Networking.Quic;
using Hive.Framework.Networking.Shared;
using System.Net;
using System.Runtime.Versioning;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Tests.Quic;

[TestFixture]
[RequiresPreviewFeatures]
public class QuicBsonTests : QuicTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new BsonPacketIdMapper();
        RegisterMessages();

        Codec = new BsonPacketCodec(PacketIdMapper);
        ClientManager = new FakeQuicClientManager();
        DataDispatcher = new DefaultDataDispatcher<QuicSession<ushort>>();

        Server = new QuicAcceptor<ushort, Guid>(_endPoint, QuicCertHelper.GenerateTestCertificate(), Codec, DataDispatcher, ClientManager);
        Server.Start();

        Client = new QuicSession<ushort>(_endPoint, Codec, DataDispatcher);
    }
}