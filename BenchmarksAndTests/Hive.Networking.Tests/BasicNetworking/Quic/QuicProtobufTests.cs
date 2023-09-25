using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Quic;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Shared.Helpers;
using System.Net;
using System.Runtime.Versioning;
using Hive.Codec.Shared;
using Hive.Framework.Networking.Udp;

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
        DataDispatcher =  new DefaultDataDispatcher<QuicSession<ushort>>();
        
        var cts = new CancellationTokenSource();
        Server = new QuicAcceptor<ushort, Guid>(_endPoint, Codec, DataDispatcher, ClientManager, new QuicSessionCreator<ushort>(Codec, DataDispatcher),
            QuicCertHelper.GenerateTestCertificate());
        Server.SetupAsync(cts.Token).Wait(cts.Token);
        Server.StartAcceptLoop(cts.Token);

        Client = new QuicSession<ushort>(_endPoint, Codec, DataDispatcher);
    }
}