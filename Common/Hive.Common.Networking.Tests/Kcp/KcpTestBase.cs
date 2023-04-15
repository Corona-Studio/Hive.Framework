using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Kcp;
using Hive.Framework.Networking.Tests.Messages;

namespace Hive.Framework.Networking.Tests.Kcp;

public abstract class KcpTestBase
{
    protected IPacketIdMapper<ushort> _packetIdMapper;
    protected KcpSession<ushort> _client;
    protected KcpAcceptor<ushort, Guid> _server;
    protected IPacketCodec<ushort> _codec;
    protected FakeKcpClientManager _clientManager;
    protected IDataDispatcher<KcpSession<ushort>> _dataDispatcher;

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