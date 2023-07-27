using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Udp;
using Hive.Framework.Shared;
using System.Net;
using Hive.Common.Codec.MemoryPack;
using Hive.Common.Codec.Shared;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Udp;

[TestFixture]
public sealed class UdpMemoryPackTests : UdpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new DefaultPacketIdMapper();
        RegisterMessages();

        Codec = new MemoryPackPacketCodec(PacketIdMapper);
        ClientManager = new FakeUdpClientManager();
        DataDispatcherProvider = () => new DefaultDataDispatcher<UdpSession<ushort>>();

        Server = new UdpAcceptor<ushort, Guid>(_endPoint, Codec, DataDispatcherProvider, ClientManager);
        Server.Start();

        Client = new UdpSession<ushort>(_endPoint, Codec, DataDispatcherProvider());
    }
}