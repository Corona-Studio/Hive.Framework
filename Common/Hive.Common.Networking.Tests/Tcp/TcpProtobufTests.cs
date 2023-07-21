using Hive.Framework.Codec.Protobuf;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Framework.Shared;
using System.Net;

namespace Hive.Framework.Networking.Tests.Tcp;

[TestFixture]
public sealed class TcpProtobufTests : TcpTestBase
{
    private readonly IPEndPoint _endPoint = IPEndPoint.Parse($"127.0.0.1:{NetworkHelper.GetRandomPort()}");

    [OneTimeSetUp]
    public void Setup()
    {
        PacketIdMapper = new ProtoBufPacketIdMapper();
        RegisterMessages();

        Codec = new ProtoBufPacketCodec(PacketIdMapper);
        ClientManager = new FakeTcpClientManager();
        DataDispatcher = new DefaultDataDispatcher<TcpSession<ushort>>();

        Server = new TcpAcceptor<ushort, Guid>(_endPoint, Codec, DataDispatcher, ClientManager);
        Server.Start();

        Client = new TcpSession<ushort>(_endPoint, Codec, DataDispatcher);
    }
}