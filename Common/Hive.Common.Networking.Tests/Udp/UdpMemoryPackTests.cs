using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Networking.Udp;
using Hive.Framework.Shared;
using System.Net;
using Hive.Common.Codec.MemoryPack;

namespace Hive.Framework.Networking.Tests.Udp;

[TestFixture]
public class UdpMemoryPackTests : UdpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new MemoryPackPacketIdMapper();
        RegisterMessages();

        Codec = new MemoryPackPacketCodec(PacketIdMapper);
        ClientManager = new FakeUdpClientManager();
        DataDispatcher = new DefaultDataDispatcher<UdpSession<ushort>>();

        Server = new UdpAcceptor<ushort, Guid>(_endPoint, Codec, DataDispatcher, ClientManager);
        Server.Start();

        Client = new UdpSession<ushort>(_endPoint, Codec, DataDispatcher);
    }
}