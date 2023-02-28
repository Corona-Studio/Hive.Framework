using Hive.Framework.Codec.Bson;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Networking.Tests.Messages;
using System.Net;

namespace Hive.Framework.Networking.Tests.Tcp;

[TestFixture]
public class TcpBsonTests : TcpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse("127.0.0.1:1100");

    [OneTimeSetUp]
    public void Setup()
    {
        _packetIdMapper = new BsonPacketIdMapper();
        _packetIdMapper.Register<HeartBeatMessage>();
        _packetIdMapper.Register<SigninMessage>();
        _packetIdMapper.Register<SignOutMessage>();
        _packetIdMapper.Register<ReconnectMessage>();

        _codec = new BsonPacketCodec(_packetIdMapper);
        _clientManager = new FakeTcpClientManager();
        _dataDispatcher = new DefaultDataDispatcher<TcpSession<ushort>>();

        _server = new TcpAcceptor<ushort, Guid>(_endPoint, _codec, _dataDispatcher, _clientManager);
        _server.Start();

        _client = new TcpSession<ushort>(_endPoint, _codec, _dataDispatcher);
    }
}