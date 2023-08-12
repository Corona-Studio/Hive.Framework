﻿using Hive.Framework.Networking.Kcp;
using Hive.Framework.Networking.Shared;
using System.Net;
using Hive.Codec.MemoryPack;
using Hive.Codec.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Kcp;

[TestFixture]
public sealed class KcpMemoryPackTests : KcpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new DefaultPacketIdMapper();
        RegisterMessages();

        Codec = new MemoryPackPacketCodec(PacketIdMapper);
        ClientManager = new FakeKcpClientManager();
        DataDispatcherProvider =  () => new DefaultDataDispatcher<KcpSession<ushort>>();

        Server = new KcpAcceptor<ushort, Guid>(_endPoint, Codec, DataDispatcherProvider, ClientManager);
        Server.Start();

        Client = new KcpSession<ushort>(_endPoint, Codec, DataDispatcherProvider());
    }
}