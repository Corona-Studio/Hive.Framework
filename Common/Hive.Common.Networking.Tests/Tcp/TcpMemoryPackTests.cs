using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Shared;
using System.Net;
using Hive.Common.Codec.MemoryPack;

namespace Hive.Framework.Networking.Tests.Tcp;

[TestFixture]
public class TcpMemoryPackTests : TcpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        _packetIdMapper = new MemoryPackPacketIdMapper();
        _packetIdMapper.Register<HeartBeatMessage>();
        _packetIdMapper.Register<SigninMessage>();
        _packetIdMapper.Register<SignOutMessage>();
        _packetIdMapper.Register<ReconnectMessage>();

        _codec = new MemoryPackPacketCodec(_packetIdMapper);
        _clientManager = new FakeTcpClientManager();
        _dataDispatcher = new DefaultDataDispatcher<TcpSession<ushort>>();

        _server = new TcpAcceptor<ushort, Guid>(_endPoint, _codec, _dataDispatcher, _clientManager);
        _server.Start();

        _client = new TcpSession<ushort>(_endPoint, _codec, _dataDispatcher);
    }
}