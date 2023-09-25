using System.Net;
using System.Net.Sockets;
using Hive.Codec.Shared;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Codec.Bson;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Udp;
using Hive.Framework.Shared.Helpers;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Udp;

public abstract class UdpTestBase :
    AbstractNetworkingTestBase<UdpSession<ushort>, Socket, UdpAcceptor<ushort, Guid>, FakeUdpClientManager>
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    public abstract IPacketCodec<ushort> CreateCodec(IPacketIdMapper<ushort> mapper);
    
    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new DefaultPacketIdMapper();
        RegisterMessages();

        Codec = CreateCodec(PacketIdMapper);
        ClientManager = new FakeUdpClientManager();
        var dispatcher = new DefaultDataDispatcher<UdpSession<ushort>>();

        var cts=new CancellationTokenSource(); 
        Server = new UdpAcceptor<ushort, Guid>(_endPoint, Codec, dispatcher, ClientManager,
            new UdpSessionCreator<ushort>(Codec,dispatcher));
        Server.SetupAsync(cts.Token);
        Server.StartAcceptLoop(cts.Token);

        Client = new UdpSession<ushort>(_endPoint, Codec, dispatcher);
    }
}