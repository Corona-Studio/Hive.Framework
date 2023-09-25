using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Shared.Helpers;
using System.Net;
using Hive.Codec.Shared;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Tcp;

[TestFixture]
public sealed class TcpProtobufTests : TcpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new DefaultPacketIdMapper();
        RegisterMessages();

        Codec = new ProtoBufPacketCodec(PacketIdMapper);
        ClientManager = new FakeTcpClientManager();
        var dispatcher = new DefaultDataDispatcher<TcpSession<ushort>>();

        var cts=new CancellationTokenSource(); 
        Server = new TcpAcceptor<ushort, Guid>(_endPoint, Codec, dispatcher, ClientManager,
            new TcpSessionCreator<ushort>(Codec,dispatcher));
        Server.SetupAsync(cts.Token);
        Server.StartAcceptLoop(cts.Token);

        Client = new TcpSession<ushort>(_endPoint, Codec, dispatcher);
    }
}