using Hive.Framework.Networking.Quic;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Shared;
using System.Net;
using System.Runtime.Versioning;
using Hive.Common.Codec.MemoryPack;

namespace Hive.Framework.Networking.Tests.Quic;

[TestFixture]
[RequiresPreviewFeatures]
public class QuicMemoryPackTests : QuicTestBase
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
        _clientManager = new FakeQuicClientManager();
        _dataDispatcher = new DefaultDataDispatcher<QuicSession<ushort>>();

        _server = new QuicAcceptor<ushort, Guid>(_endPoint, QuicCertHelper.GenerateTestCertificate(), _codec, _dataDispatcher, _clientManager);
        _server.Start();

        _client = new QuicSession<ushort>(_endPoint, _codec, _dataDispatcher);
    }
}