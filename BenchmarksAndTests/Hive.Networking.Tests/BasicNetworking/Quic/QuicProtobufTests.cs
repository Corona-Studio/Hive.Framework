using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Quic;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Shared.Helpers;
using System.Net;
using System.Runtime.Versioning;
using Hive.Codec.Shared;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Quic;

[TestFixture]
[RequiresPreviewFeatures]
public sealed class QuicProtobufTests : QuicTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new DefaultPacketIdMapper();
        RegisterMessages();

        Codec = new ProtoBufPacketCodec(PacketIdMapper);
        ClientManager = new FakeQuicClientManager();
        DataDispatcherProvider = () => new DefaultDataDispatcher<QuicSession<ushort>>();

        Server = new QuicAcceptor<ushort, Guid>(_endPoint, QuicCertHelper.GenerateTestCertificate(), Codec, DataDispatcherProvider, ClientManager);
        Server.Start();

        Client = new QuicSession<ushort>(_endPoint, Codec, DataDispatcherProvider());
    }
}