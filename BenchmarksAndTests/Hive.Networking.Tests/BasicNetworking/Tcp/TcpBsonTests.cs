﻿using Hive.Common.Codec.Shared;
using Hive.Framework.Codec.Bson;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Shared.Helpers;
using System.Net;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Tcp;

[TestFixture]
public sealed class TcpBsonTests : TcpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new DefaultPacketIdMapper();
        RegisterMessages();

        Codec = new BsonPacketCodec(PacketIdMapper);
        ClientManager = new FakeTcpClientManager();
        DataDispatcherProvider = () => new DefaultDataDispatcher<TcpSession<ushort>>();

        Server = new TcpAcceptor<ushort, Guid>(_endPoint, Codec, DataDispatcherProvider, ClientManager);
        Server.Start();

        Client = new TcpSession<ushort>(_endPoint, Codec, DataDispatcherProvider());
    }
}