using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Quic;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Shared;
using System.Net;
using System.Runtime.Versioning;

namespace Hive.Framework.Networking.Tests.Quic;

[TestFixture]
[RequiresPreviewFeatures]
public class QuicProtobufTests : QuicTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new ProtoBufPacketIdMapper();
        RegisterMessages();

        Codec = new ProtoBufPacketCodec(PacketIdMapper);
        ClientManager = new FakeQuicClientManager();
        DataDispatcher = new DefaultDataDispatcher<QuicSession<ushort>>();

        Server = new QuicAcceptor<ushort, Guid>(_endPoint, QuicCertHelper.GenerateTestCertificate(), Codec, DataDispatcher, ClientManager);
        Server.Start();

        Client = new QuicSession<ushort>(_endPoint, Codec, DataDispatcher);
    }
}