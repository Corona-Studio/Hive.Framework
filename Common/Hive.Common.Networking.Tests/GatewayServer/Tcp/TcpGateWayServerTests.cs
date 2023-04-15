using System.Net;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.LoadBalancers;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Networking.Tests.Tcp;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Tests.GatewayServer.Tcp;

[TestFixture]
public class TcpGateWayServerTests
{
    private IPacketIdMapper<ushort> _packetIdMapper;
    private IPacketCodec<ushort> _clientPacketCodec;
    private IPacketCodec<ushort> _serverPacketCodec;
    private FakeTcpGatewayServer _gatewayServer;

    private IDataDispatcher<TcpSession<ushort>> _gatewayServerDataDispatcher;
    private IDataDispatcher<TcpSession<ushort>> _serverDataDispatcher;
    private IDataDispatcher<TcpSession<ushort>> _dataDispatcher1;
    private IDataDispatcher<TcpSession<ushort>> _dataDispatcher2;

    private TcpSession<ushort> _client1;
    private TcpSession<ushort> _client2;
    private TcpSession<ushort> _server;
    
    private readonly IPEndPoint _gatewayServerEndPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeTearDown]
    public void TearDown()
    {
        _gatewayServer.Dispose();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        _packetIdMapper = new ProtoBufPacketIdMapper();
        _packetIdMapper.Register<HeartBeatMessage>();
        _packetIdMapper.Register<SigninMessage>();
        _packetIdMapper.Register<SignOutMessage>();
        _packetIdMapper.Register<ReconnectMessage>();
        _packetIdMapper.Register<ServerRegistrationMessage>();
        _packetIdMapper.Register<ClientStartTransmitMessage>();
        _packetIdMapper.Register<ClientCanTransmitMessage>();
        _packetIdMapper.Register<ServerRedirectTestMessage1>();

        _clientPacketCodec = new ProtoBufPacketCodec(_packetIdMapper);
        _serverPacketCodec = new ProtoBufPacketCodec(_packetIdMapper, new IPacketPrefixResolver[]
        {
            new ClientIdPrefixResolver()
        });

        _gatewayServerDataDispatcher = new DefaultDataDispatcher<TcpSession<ushort>>();
        _serverDataDispatcher = new DefaultDataDispatcher<TcpSession<ushort>>();
        _dataDispatcher1 = new DefaultDataDispatcher<TcpSession<ushort>>();
        _dataDispatcher2 = new DefaultDataDispatcher<TcpSession<ushort>>();

        _gatewayServer = new FakeTcpGatewayServer(
            _clientPacketCodec,
            new TcpAcceptor<ushort, Guid>(_gatewayServerEndPoint, _clientPacketCodec, _gatewayServerDataDispatcher, new FakeTcpClientManager()),
            session =>
            {
                var lb = new BasicLoadBalancer<TcpSession<ushort>>();

                lb.AddSession(session);

                return lb;
            });

        _gatewayServer.StartServer();

        Task.Delay(3000).Wait();

        _server = new TcpSession<ushort>(_gatewayServerEndPoint, _serverPacketCodec, _serverDataDispatcher);
        _client1 = new TcpSession<ushort>(_gatewayServerEndPoint, _clientPacketCodec, _dataDispatcher1);
        _client2 = new TcpSession<ushort>(_gatewayServerEndPoint, _clientPacketCodec, _dataDispatcher2);
    }

    [Test]
    [Order(1)]
    public async Task ServerRegistrationTest()
    {
        await Task.Delay(2000);

        _server.Send(new SigninMessage{ Id = 114514 });

        await Task.Delay(1500);

        _server.Send(new ServerRegistrationMessage
        {
            PackagesToReceive = new []
            {
                _packetIdMapper.GetPacketId(typeof(ServerRedirectTestMessage1))
            }
        });

        await Task.Delay(500);

        Assert.That(_gatewayServer.RegisteredForwardPacketCount, Is.EqualTo(1));
    }
}