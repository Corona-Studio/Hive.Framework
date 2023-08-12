using Hive.Framework.Networking.Quic;
using Hive.Framework.Networking.Shared;
using System.Net;
using System.Runtime.Versioning;
using Hive.Codec.MemoryPack;
using Hive.Codec.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Quic;

[TestFixture]
[RequiresPreviewFeatures]
public sealed class QuicMemoryPackTests : QuicTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new DefaultPacketIdMapper();
        RegisterMessages();

        Codec = new MemoryPackPacketCodec(PacketIdMapper);
        ClientManager = new FakeQuicClientManager();
        DataDispatcherProvider = () => new DefaultDataDispatcher<QuicSession<ushort>>();

        Server = new QuicAcceptor<ushort, Guid>(_endPoint, QuicCertHelper.GenerateTestCertificate(), Codec, DataDispatcherProvider, ClientManager);
        Server.Start();

        Client = new QuicSession<ushort>(_endPoint, Codec, DataDispatcherProvider());
    }
}