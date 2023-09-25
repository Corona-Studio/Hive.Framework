using System.Net;
using System.Net.Sockets;
using Hive.Codec.Shared;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Kcp;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Udp;
using Hive.Framework.Shared.Helpers;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Kcp;

public abstract class KcpTestBase :
    AbstractNetworkingTestBase<KcpSession<ushort>, Socket, KcpAcceptor<ushort, Guid>, FakeKcpClientManager>
{
    public abstract IPacketCodec<ushort> CreateCodec(IPacketIdMapper<ushort> mapper);
    
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new DefaultPacketIdMapper();
        RegisterMessages();

        Codec = CreateCodec(PacketIdMapper);
        ClientManager = new FakeKcpClientManager();
        var dispatcher =  new DefaultDataDispatcher<KcpSession<ushort>>();
        
        var cts = new CancellationTokenSource();
        Server = new KcpAcceptor<ushort, Guid>(_endPoint, Codec, dispatcher, ClientManager, new KcpSessionCreator<ushort>(Codec, dispatcher));
        Server.SetupAsync(cts.Token).Wait(cts.Token);
        Server.StartAcceptLoop(cts.Token);

        Client = new KcpSession<ushort>(_endPoint, Codec, dispatcher);
    }
}