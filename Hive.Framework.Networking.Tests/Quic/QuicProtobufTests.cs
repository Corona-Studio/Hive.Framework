using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Quic;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Shared;
using System.Net;
using System.Runtime.Versioning;

namespace Hive.Framework.Networking.Tests.Quic;

[TestFixture]
[RequiresPreviewFeatures]
public class QuicProtobufTests : QuicTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        _packetIdMapper = new ProtoBufPacketIdMapper();
        _packetIdMapper.Register<HeartBeatMessage>();
        _packetIdMapper.Register<SigninMessage>();
        _packetIdMapper.Register<SignOutMessage>();
        _packetIdMapper.Register<ReconnectMessage>();

        _codec = new ProtoBufPacketCodec(_packetIdMapper);
        _clientManager = new FakeQuicClientManager();
        _dataDispatcher = new DefaultDataDispatcher<QuicSession<ushort>>();

        _server = new QuicAcceptor<ushort, Guid>(_endPoint, QuicCertHelper.GenerateTestCertificate(), _codec, _dataDispatcher, _clientManager);
        _server.Start();

        _client = new QuicSession<ushort>(_endPoint, _codec, _dataDispatcher);
    }
}