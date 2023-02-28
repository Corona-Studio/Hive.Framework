﻿using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Networking.Udp;
using System.Net;
using Hive.Framework.Codec.Bson;

namespace Hive.Framework.Networking.Tests.Udp;

[TestFixture]
public class UdpBsonTests : UdpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse("127.0.0.1:1201");

    [OneTimeSetUp]
    public void Setup()
    {
        _packetIdMapper = new BsonPacketIdMapper();
        _packetIdMapper.Register<HeartBeatMessage>();
        _packetIdMapper.Register<SigninMessage>();
        _packetIdMapper.Register<SignOutMessage>();
        _packetIdMapper.Register<ReconnectMessage>();

        _codec = new BsonPacketCodec(_packetIdMapper);
        _clientManager = new FakeUdpClientManager();
        _dataDispatcher = new DefaultDataDispatcher<UdpSession<ushort>>();

        _server = new UdpAcceptor<ushort, Guid>(_endPoint, _codec, _dataDispatcher, _clientManager);
        _server.Start();

        _client = new UdpSession<ushort>(_endPoint, _codec, _dataDispatcher);
    }
}