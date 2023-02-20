using System.Net;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Networking.Tests.Messages;

namespace Hive.Framework.Networking.Tests.Tcp;

public class TcpTests
{
    private IPacketIdMapper<ushort> _packetIdMapper;
    private TcpSession<ushort> _client;
    private TcpAcceptor<ushort, Guid> _server;
    private IPacketCodec<ushort> _codec;
    private FakeTcpClientManager _clientManager;
    private IDataDispatcher<TcpSession<ushort>> _dataDispatcher;

    private readonly IPEndPoint _endPoint = IPEndPoint.Parse("127.0.0.1:1234");

    [OneTimeSetUp]
    public void Setup()
    {
        _packetIdMapper = new ProtoBufPacketIdMapper();
        _packetIdMapper.Register<HeartBeatMessage>();
        _packetIdMapper.Register<SigninMessage>();
        _packetIdMapper.Register<SignOutMessage>();

        _codec = new ProtoBufPackerCodec(_packetIdMapper);
        _clientManager = new FakeTcpClientManager();
        _dataDispatcher = new DefaultDataDispatcher<TcpSession<ushort>>();

        _server = new TcpAcceptor<ushort, Guid>(_endPoint, _codec, _dataDispatcher, _clientManager);
        _server.Start();

        _client = new TcpSession<ushort>(_endPoint, _codec, _dataDispatcher);
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
        Assert.That(_clientManager.ConnectedClient, Is.EqualTo(0));

        _client.Send(new SigninMessage { Id = 114514 });

        await Task.Delay(100);

        Assert.That(_clientManager.SigninMessageVal, Is.EqualTo(114514));
        Assert.That(_clientManager.ConnectedClient, Is.EqualTo(1));
    }

    [Test]
    [Order(2)]
    public async Task SignOutTest()
    {
        Assert.That(_clientManager.ConnectedClient, Is.EqualTo(1));

        _client.Send(new SignOutMessage { Id = 1919870 });

        await Task.Delay(100);

        Assert.That(_clientManager.SignOutMessageVal, Is.EqualTo(1919870));
        Assert.That(_clientManager.ConnectedClient, Is.EqualTo(0));
    }
}