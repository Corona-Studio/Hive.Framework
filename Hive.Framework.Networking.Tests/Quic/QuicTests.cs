using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tests.Messages;
using System.Net;
using System.Runtime.Versioning;
using Hive.Framework.Networking.Quic;

namespace Hive.Framework.Networking.Tests.Quic;

[RequiresPreviewFeatures]
public class QuicTests
{
    private IPacketIdMapper<ushort> _packetIdMapper;
    private QuicSession<ushort> _client;
    private QuicAcceptor<ushort, Guid> _server;
    private IPacketCodec<ushort> _codec;
    private FakeQuicClientManager _clientManager;
    private IDataDispatcher<QuicSession<ushort>> _dataDispatcher;

    private readonly IPEndPoint _endPoint = IPEndPoint.Parse("127.0.0.1:1234");

    [OneTimeSetUp]
    public void Setup()
    {
        _packetIdMapper = new ProtoBufPacketIdMapper();
        _packetIdMapper.Register<HeartBeatMessage>();
        _packetIdMapper.Register<SigninMessage>();
        _packetIdMapper.Register<SignOutMessage>();
        _packetIdMapper.Register<ReconnectMessage>();

        _codec = new ProtoBufPackerCodec(_packetIdMapper);
        _clientManager = new FakeQuicClientManager();
        _dataDispatcher = new DefaultDataDispatcher<QuicSession<ushort>>();

        _server = new QuicAcceptor<ushort, Guid>(_endPoint, QuicCertHelper.GenerateTestCertificate(), _codec, _dataDispatcher, _clientManager);
        _server.Start();

        _client = new QuicSession<ushort>(_endPoint, _codec, _dataDispatcher);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Test]
    [Order(1)]
    public async Task SigninTest()
    {
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.That(_clientManager.ConnectedClient, Is.EqualTo(0));

        _client.Send(new SigninMessage { Id = 114514 });

        await Task.Delay(3000);

        Assert.That(_clientManager.SigninMessageVal, Is.EqualTo(114514));
        Assert.That(_clientManager.ConnectedClient, Is.EqualTo(1));
    }

    [Test]
    [Order(2)]
    public async Task ReconnectTest()
    {
        await Task.Delay(TimeSpan.FromSeconds(10));

        Assert.That(_clientManager.DisconnectedClient, Is.EqualTo(1));

        _client.Send(new ReconnectMessage());

        await Task.Delay(100);

        Assert.That(_clientManager.ReconnectedClient, Is.EqualTo(1));
    }

    [Test]
    [Order(3)]
    public async Task SignOutTest()
    {
        Assert.That(_clientManager.ConnectedClient, Is.EqualTo(1));

        _client.Send(new SignOutMessage { Id = 1919870 });

        await Task.Delay(100);

        Assert.That(_clientManager.SignOutMessageVal, Is.EqualTo(1919870));
        Assert.That(_clientManager.ConnectedClient, Is.EqualTo(0));
    }
}