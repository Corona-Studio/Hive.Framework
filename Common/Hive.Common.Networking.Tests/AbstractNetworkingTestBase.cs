using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Helpers;
using Hive.Framework.Networking.Tests.Messages;

namespace Hive.Framework.Networking.Tests;

public interface INetworkingTestProperties
{
    public int ConnectedClient { get; }
    public int SigninMessageVal { get; }
    public int SignOutMessageVal { get; }
    public int ReconnectedClient { get; }
    public int DisconnectedClient { get; }
    public int AdderCount { get; }
    public int AdderPackageReceiveCount { get; }
}

public abstract class AbstractNetworkingTestBase<TSession, TClient, TAcceptor, TClientManager> 
    where TSession : AbstractSession<ushort, TSession>
    where TAcceptor : AbstractAcceptor<TClient, TSession, ushort, Guid>
    where TClientManager : AbstractClientManager<Guid, TSession>, INetworkingTestProperties

{
    protected IPacketIdMapper<ushort> PacketIdMapper = null!;
    protected TSession Client = null!;
    protected TAcceptor Server = null!;
    protected IPacketCodec<ushort> Codec = null!;
    protected TClientManager ClientManager = null!;
    protected IDataDispatcher<TSession> DataDispatcher = null!;

    private bool ShouldSendHeartBeat { get; set; } = true;
    private void StartHeartBeat()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(1000);

                if (!ShouldSendHeartBeat) continue;

                Client.Send(new HeartBeatMessage());
            }
        });
    }

    protected void RegisterMessages()
    {
        PacketIdMapper.Register<HeartBeatMessage>();
        PacketIdMapper.Register<SigninMessage>();
        PacketIdMapper.Register<SignOutMessage>();
        PacketIdMapper.Register<ReconnectMessage>();
        PacketIdMapper.Register<CountTestMessage>();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Client.Dispose();
        Server.Dispose();
    }

    [Test]
    [Order(1)]
    public async Task SigninTest()
    {
        Assert.That(ClientManager.ConnectedClient, Is.EqualTo(0));

        await SpinWaitAsync.SpinUntil(() => Client.CanSend);

        Client.Send(new SigninMessage { Id = 114514 });

        await Task.Delay(100);

        Assert.Multiple(() =>
        {
            Assert.That(ClientManager.SigninMessageVal, Is.EqualTo(114514));
            Assert.That(ClientManager.ConnectedClient, Is.EqualTo(1));
        });

        StartHeartBeat();
    }

    [Test]
    [Order(2)]
    public async Task HeartBeatTest()
    {
        await Task.Delay(TimeSpan.FromSeconds(35));

        Assert.Multiple(() =>
        {
            Assert.That(ClientManager.DisconnectedClient, Is.EqualTo(0));
            Assert.That(ClientManager.ConnectedClient, Is.EqualTo(1));
        });
    }

    [Test]
    [Order(3)]
    public async Task MessageReceiveTest()
    {
        await Task.Delay(1000);

        const int times = 100;
        var realResult = 0;

        for (var i = 0; i < times; i++)
        {
            realResult += i;

            Client.Send(new CountTestMessage { Adder = i });
            await Task.Delay(10);
        }

        await Task.Delay(3000);

        Assert.Multiple(() =>
        {
            Assert.That(ClientManager.AdderPackageReceiveCount, Is.EqualTo(times));
            Assert.That(ClientManager.AdderCount, Is.EqualTo(realResult));
        });
    }

    [Test]
    [Order(4)]
    public async Task ReconnectTest()
    {
        ShouldSendHeartBeat = false;

        await Task.Delay(TimeSpan.FromSeconds(35));

        Assert.That(ClientManager.DisconnectedClient, Is.EqualTo(1));

        if (Client.ShouldDestroyAfterDisconnected)
        {
            await Client.DoConnect();
            await Task.Delay(500);
        }

        Client.Send(new ReconnectMessage());

        await Task.Delay(500);

        Assert.That(ClientManager.ReconnectedClient, Is.EqualTo(1));
    }

    [Test]
    [Order(5)]
    public async Task SignOutTest()
    {
        Assert.That(ClientManager.ConnectedClient, Is.EqualTo(1));

        Client.Send(new SignOutMessage { Id = 1919870 });

        await Task.Delay(100);

        Assert.Multiple(() =>
        {
            Assert.That(ClientManager.SignOutMessageVal, Is.EqualTo(1919870));
            Assert.That(ClientManager.ConnectedClient, Is.EqualTo(0));
        });
    }
}