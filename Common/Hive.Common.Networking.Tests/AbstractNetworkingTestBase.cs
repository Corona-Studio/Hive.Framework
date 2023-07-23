using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Helpers;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Networking.Tests.Messages.BidirectionalPacket;

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
    public int BidirectionalPacketAddResult { get; }
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
    protected Func<IDataDispatcher<TSession>> DataDispatcherProvider = null!;

    private bool ShouldSendHeartBeat { get; set; } = true;
    private void StartHeartBeat()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                if (!ShouldSendHeartBeat)
                {
                    await Task.Delay(1);
                    continue;
                }

                await Client.Send(new HeartBeatMessage());
                await Task.Delay(100);
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
        PacketIdMapper.Register<C2STestPacket>();
        PacketIdMapper.Register<S2CTestPacket>();
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

        await Client.Send(new SigninMessage { Id = 114514 });

        await Task.Delay(100);

        Assert.Multiple(() =>
        {
            Assert.That(ClientManager.SigninMessageVal, Is.EqualTo(114514));
            Assert.That(ClientManager.ConnectedClient, Is.EqualTo(1));
        });
    }

    [Test]
    [Order(2)]
    public async Task HeartBeatTest()
    {
        StartHeartBeat();

        await Task.Delay(TimeSpan.FromSeconds(30));

        Assert.Multiple(() =>
        {
            Assert.That(ClientManager.DisconnectedClient, Is.EqualTo(0));
            Assert.That(ClientManager.ConnectedClient, Is.EqualTo(1));
        });
    }

    const int SendTimes = 100;

    [Test]
    [Order(3)]
    public async Task MessageReceiveTest()
    {
        await Task.Delay(1000);

        var realResult = 0;

        for (var i = 0; i < SendTimes; i++)
        {
            realResult += i;

            await Client.Send(new CountTestMessage { Adder = i });
            await Task.Delay(10);
        }

        await Task.Delay(3000);

        Assert.Multiple(() =>
        {
            Assert.That(ClientManager.AdderPackageReceiveCount, Is.EqualTo(SendTimes));
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

        await Client.Send(new ReconnectMessage());

        await Task.Delay(500);

        ShouldSendHeartBeat = true;

        Assert.That(ClientManager.ReconnectedClient, Is.EqualTo(1));
    }

    [Test]
    [Order(5)]
    public async Task ReconnectedMessageReceiveTest()
    {
        await Task.Delay(1000);

        for (var i = 0; i < SendTimes; i++)
        {
            await Client.Send(new CountTestMessage { Adder = -i });
            await Task.Delay(10);
        }

        await Task.Delay(1000);

        Assert.Multiple(() =>
        {
            Assert.That(ClientManager.AdderPackageReceiveCount, Is.EqualTo(SendTimes * 2));
            Assert.That(ClientManager.AdderCount, Is.EqualTo(0));
        });
    }

    [Test]
    [Order(6)]
    public async Task BidirectionalPacketSendReceiveTest()
    {
        await Task.Delay(1000);

        var receivedCount = 0;

        Client.OnReceive<S2CTestPacket>((message, _) =>
        {
            Console.WriteLine(message.ReversedRandomNumber);
            receivedCount += message.ReversedRandomNumber;
        });

        for (var i = 0; i < SendTimes; i++)
        {
            await Client.Send(new C2STestPacket { RandomNumber = i });
            await Task.Delay(10);
        }

        await Task.Delay(6000);

        Assert.That(receivedCount + ClientManager.BidirectionalPacketAddResult, Is.EqualTo(0));
    }

    [Test]
    [Order(7)]
    public async Task SignOutTest()
    {
        Assert.That(ClientManager.ConnectedClient, Is.EqualTo(1));

        await Client.Send(new SignOutMessage { Id = 1919870 });

        await Task.Delay(3000);

        Assert.Multiple(() =>
        {
            Assert.That(ClientManager.SignOutMessageVal, Is.EqualTo(1919870));
            Assert.That(ClientManager.ConnectedClient, Is.EqualTo(0));
        });
    }
}