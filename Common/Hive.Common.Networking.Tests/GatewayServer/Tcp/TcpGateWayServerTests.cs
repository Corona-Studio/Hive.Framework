using System.Net;
using Hive.Common.Codec.Shared;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.LoadBalancers;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Networking.Tests.BasicNetworking;
using Hive.Framework.Networking.Tests.BasicNetworking.Tcp;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Collections;

namespace Hive.Framework.Networking.Tests.GatewayServer.Tcp;

[TestFixture]
public class TcpGateWayServerTests
{
    private IPacketIdMapper<ushort> _packetIdMapper = null!;
    private IPacketCodec<ushort> _clientPacketCodec = null!;
    private IPacketCodec<ushort> _serverPacketCodec = null!;
    private FakeTcpGatewayServer _gatewayServer = null!;

    private Func<IDataDispatcher<TcpSession<ushort>>> _gatewayServerDataDispatcherProvider = null!;

    private TcpSession<ushort> _client1 = null!;
    private TcpSession<ushort> _client2 = null!;
    private TcpSession<ushort> _server = null!;
    
    private readonly IPEndPoint _gatewayServerEndPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    private INetworkingTestProperties GatewayServerTestProperties => (INetworkingTestProperties)_gatewayServer.Acceptor.ClientManager;

    private bool ShouldSendHeartBeat { get; set; } = true;
    private void StartServerHeartBeat()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                if (!ShouldSendHeartBeat)
                {
                    await Task.Delay(1);
                    continue;
                }

                await _server.SendAsync(new HeartBeatMessage());
                
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        });
    }

    private void StartClientHeartBeat()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                if (!ShouldSendHeartBeat)
                {
                    await Task.Delay(1);
                    continue;
                }
                
                await _client1.SendAsync(new HeartBeatMessage());
                await _client2.SendAsync(new HeartBeatMessage());

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        });
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _gatewayServer.Dispose();
    }

    [OneTimeSetUp]
    public void Setup()
    {
        _packetIdMapper = new DefaultPacketIdMapper();
        _packetIdMapper.Register<HeartBeatMessage>();
        _packetIdMapper.Register<SigninMessage>();
        _packetIdMapper.Register<SignOutMessage>();
        _packetIdMapper.Register<ReconnectMessage>();
        _packetIdMapper.Register<ServerRegistrationMessage>();
        _packetIdMapper.Register<ClientStartTransmitMessage>();
        _packetIdMapper.Register<ClientCanTransmitMessage>();
        _packetIdMapper.Register<ServerRedirectTestMessage1>();
        _packetIdMapper.Register<ServerRedirectTestMessage2>();
        _packetIdMapper.Register<ServerReplyTestMessage1>();
        _packetIdMapper.Register<DefaultServerReplyPacket>();

        _clientPacketCodec = new ProtoBufPacketCodec(_packetIdMapper);
        _serverPacketCodec = new ProtoBufPacketCodec(_packetIdMapper, new IPacketPrefixResolver[]
        {
            new ClientIdPrefixResolver()
        });

        _gatewayServerDataDispatcherProvider = () => new DefaultDataDispatcher<TcpSession<ushort>>();

        _gatewayServer = new FakeTcpGatewayServer(
            _clientPacketCodec,
            new TcpAcceptor<ushort, Guid>(
                _gatewayServerEndPoint,
                _clientPacketCodec,
                _gatewayServerDataDispatcherProvider,
                new FakeTcpClientManager()),
            session =>
            {
                var lb = new BasicLoadBalancer<TcpSession<ushort>>();

                lb.AddSession(session);

                return lb;
            });

        _gatewayServer.StartServer();

        Task.Delay(3000).Wait();

        _server = new TcpSession<ushort>(_gatewayServerEndPoint, _serverPacketCodec, _gatewayServerDataDispatcherProvider());
        _client1 = new TcpSession<ushort>(_gatewayServerEndPoint, _clientPacketCodec, _gatewayServerDataDispatcherProvider());
        _client2 = new TcpSession<ushort>(_gatewayServerEndPoint, _clientPacketCodec, _gatewayServerDataDispatcherProvider());

        _redirectIds = new[]
        {
            _packetIdMapper.GetPacketId(typeof(ServerRedirectTestMessage1)),
            _packetIdMapper.GetPacketId(typeof(ServerRedirectTestMessage2))
        };
    }

    private ushort[] _redirectIds = null!;

    [Test]
    [Order(1)]
    public async Task ServerRegistrationTest()
    {
        await _server.SendAsync(new SigninMessage());

        await Task.Delay(100);

        StartServerHeartBeat();

        await Task.Delay(1500);
        
        await _server.SendAsync(new ServerRegistrationMessage
        {
            PackagesToReceive = _redirectIds
        });

        await Task.Delay(500);

        Assert.Multiple(() =>
        {
            Assert.That(_gatewayServer.RegisteredForwardPacketCount, Is.EqualTo(_redirectIds.Length));
            Assert.That(GatewayServerTestProperties.ConnectedClient, Is.EqualTo(1));
        });
    }

    [Test]
    [Order(2)]
    public async Task ClientSigninTest()
    {
        await _client1.SendAsync(new SigninMessage());
        await _client2.SendAsync(new SigninMessage());

        await Task.Delay(100);

        StartClientHeartBeat();

        await Task.Delay(5000);

        Assert.That(GatewayServerTestProperties.ConnectedClient, Is.EqualTo(3));
    }

    [Test]
    [Order(3)]
    public async Task HeartBeatTest()
    {
        await Task.Delay(TimeSpan.FromSeconds(30));

        Assert.Multiple(() =>
        {
            Assert.That(GatewayServerTestProperties.DisconnectedClient, Is.EqualTo(0));
            Assert.That(GatewayServerTestProperties.ConnectedClient, Is.EqualTo(3));
        });
    }

    [Test]
    [Order(4)]
    public async Task DataTransmitHandshakeTest()
    {
        var clientsCanTransmit = 0;

        _client1.OnReceive<ClientCanTransmitMessage>((_, _) =>
        {
            clientsCanTransmit++;
        });

        _client2.OnReceive<ClientCanTransmitMessage>((_, _) =>
        {
            clientsCanTransmit++;
        });

        await Task.Delay(100);

        await _client1.SendAsync(new ClientStartTransmitMessage
        {
            RedirectPacketIds = _redirectIds
        });

        await _client2.SendAsync(new ClientStartTransmitMessage
        {
            RedirectPacketIds = _redirectIds
        });

        await Task.Delay(TimeSpan.FromSeconds(5));

        Assert.That(clientsCanTransmit, Is.EqualTo(2));
    }

    private readonly BiDictionary<Guid, string> _receiveDictionary = new();

    [Test]
    [Order(5)]
    public async Task ServerMessageReceiveTest()
    {
        lock (_receiveDictionary)
            _receiveDictionary.Clear();

        _server.OnReceive<ServerRedirectTestMessage1>((message1, _) =>
        {
            lock(_receiveDictionary)
                _receiveDictionary.Add((Guid)message1.Prefixes[0]!, message1.Payload.Content!);
        });

        await Task.Delay(100);

        await _client1.SendAsync(new ServerRedirectTestMessage1 { Content = "pp" });
        await _client2.SendAsync(new ServerRedirectTestMessage1 { Content = "123" });

        await Task.Delay(TimeSpan.FromSeconds(2));

        lock (_receiveDictionary)
            Assert.That(_receiveDictionary.Values, Is.SubsetOf(new[] { "pp", "123" }));

        _server.DataDispatcher.UnregisterAll<ServerRedirectTestMessage1>();
    }

    [Test]
    [Order(6)]
    public async Task RandomizedMessageSendTest()
    {
        var listOfGuid =
            Enumerable.Repeat(0, 100).Select(_ => Guid.NewGuid().ToString()).ToArray();

        var testStrList = new List<string>();

        _server.OnReceive<ServerRedirectTestMessage1>((message1, _) =>
        {
            testStrList.Add(message1.Payload.Content!);
        });

        await Task.Delay(100);

        foreach (var guid in listOfGuid)
        {
            var client = Random.Shared.Next(2) == 1 ? _client1 : _client2;

            await client.SendAsync(new ServerRedirectTestMessage1{Content = guid});
            await Task.Delay(10);
        }

        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.That(testStrList, Is.SubsetOf(listOfGuid));

        _server.DataDispatcher.UnregisterAll<ServerRedirectTestMessage1>();
    }

    [Test]
    [Order(7)]
    public async Task ClientMessageReceiveTest()
    {
        var client1LocalCounter = 0;
        var client2LocalCounter = 0;

        var client1Counter = 0L;
        var client2Counter = 0L;

        const int packetSendCount = 100;
        var packetReceived = 0;

        _server.OnReceive<ServerRedirectTestMessage2>(async (message, session) =>
        {
            packetReceived++;

            Console.WriteLine($"[{packetReceived}] <{(Guid)message.Prefixes[0]!}>");

            await session.SendAsync(
                new DefaultServerReplyPacket
                {
                    SendTo = (Guid)message.Prefixes[0]!,
                    InnerPayload = session.PacketCodec.Encode(new ServerRedirectTestMessage2
                        { Value = message.Payload.Value })
                });
        });

        _client1.OnReceive<ServerRedirectTestMessage2>((message, _) =>
        {
            Interlocked.Add(ref client1Counter, message.Payload.Value);
        });

        _client2.OnReceive<ServerRedirectTestMessage2>((message, _) =>
        {
            Interlocked.Add(ref client2Counter, message.Payload.Value);
        });

        await Task.Delay(100);

        for (var i = 0; i < packetSendCount; i++)
        {
            var flag = Random.Shared.Next(0, 2) == 1;
            var rnd = Random.Shared.Next();
            var client = flag ? _client1 : _client2;

            if (flag)
                client1LocalCounter += rnd;
            else
                client2LocalCounter += rnd;

            await client.SendAsync(new ServerRedirectTestMessage2
            {
                Value = rnd
            });

            await Task.Delay(10);
        }

        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.That(packetReceived, Is.EqualTo(packetSendCount));

        await Task.Delay(TimeSpan.FromSeconds(15));

        Assert.Multiple(() =>
        {
            Assert.That(client1Counter, Is.EqualTo(client1LocalCounter));
            Assert.That(client2Counter, Is.EqualTo(client2LocalCounter));
        });
    }
}