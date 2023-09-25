using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Shared.Helpers;
using Hive.Framework.Networking.Tests.Messages;
using Hive.Framework.Networking.Tests.Messages.BidirectionalPacket;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.Framework.Networking.Tests.BasicNetworking;

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
    public int NoPayloadPacketCount { get; }
}

public abstract class AbstractNetworkingTestBase<TSession, TClient, TAcceptor, TClientManager> 
    where TSession : AbstractSession<ushort, TSession>
    where TAcceptor : AbstractAcceptor<TClient, TSession, ushort, Guid>
    where TClientManager : AbstractClientManager<Guid, TSession>, INetworkingTestProperties

{
    protected IPacketIdMapper<ushort> PacketIdMapper;
    protected TClientManager ClientManager;
    protected IDataDispatcher<TSession> DataDispatcher;
    protected TSession Client;
    protected TAcceptor Server;
    protected IPacketCodec<ushort> Codec;
    

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

                await Client.SendAsync(new HeartBeatMessage(), PacketFlags.None);
                await Task.Delay(TimeSpan.FromSeconds(10));
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
        Server.CloseAsync(CancellationToken.None).Wait();
    }

    [Test]
    [Order(1)]
    public async Task SigninTest()
    {
        Assert.That(ClientManager.ConnectedClient, Is.EqualTo(0));

        await SpinWaitAsync.SpinUntil(() => Client.CanSend);

        await Client.SendAsync(new SigninMessage { Id = 114514 }, PacketFlags.None);

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

    private const int SendTimes = 100;

    [Test]
    [Order(3)]
    public async Task MessageReceiveTest()
    {
        await Task.Delay(1000);

        var realResult = 0;

        for (var i = 0; i < SendTimes; i++)
        {
            realResult += i;

            await Client.SendAsync(new CountTestMessage { Adder = i }, PacketFlags.None);
            await Task.Delay(10);
        }

        await Task.Delay(1500);

        Assert.Multiple(() =>
        {
            Assert.That(ClientManager.AdderPackageReceiveCount, Is.EqualTo(SendTimes));
            Assert.That(ClientManager.AdderCount, Is.EqualTo(realResult));
        });
    }

    [Test]
    [Order(4)]
    public async Task NoPayloadPacketReceiveTest()
    {
        await Task.Delay(1000);

        var sentCount = 0;

        for (var i = -1; i <= 30; i++)
        {
            await Client.SendWithoutPayload(PacketFlags.None);
            sentCount++;
        }

        await Task.Delay(1500);

        Assert.Multiple(() =>
        {
            Assert.That((uint)ClientManager.NoPayloadPacketCount, Is.EqualTo(sentCount));
        });
    }

    [Test]
    [Order(5)]
    public async Task ReconnectTest()
    {
        ShouldSendHeartBeat = false;

        await Task.Delay(TimeSpan.FromSeconds(45));

        Assert.That(ClientManager.DisconnectedClient, Is.EqualTo(1));

        if (Client.ShouldDestroyAfterDisconnected)
        {
            await Client.DoConnect();
            await Task.Delay(500);
        }

        await Client.SendAsync(new ReconnectMessage(), PacketFlags.None);

        await Task.Delay(500);

        ShouldSendHeartBeat = true;

        await Task.Delay(1500);

        Assert.That(ClientManager.ReconnectedClient, Is.EqualTo(1));
    }

    [Test]
    [Order(6)]
    public async Task ReconnectedMessageReceiveTest()
    {
        await Task.Delay(1000);

        for (var i = 0; i < SendTimes; i++)
        {
            await Client.SendAsync(new CountTestMessage { Adder = -i }, PacketFlags.None);
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
    [Order(7)]
    public async Task BidirectionalPacketSendReceiveTest()
    {
        await Task.Delay(1000);

        var receivedCount = 0;

        Client.OnReceive<S2CTestPacket>((message, _) =>
        {
            Console.WriteLine(message.Payload.ReversedRandomNumber);
            receivedCount += message.Payload.ReversedRandomNumber;
        });

        for (var i = 0; i < SendTimes; i++)
        {
            await Client.SendAsync(new C2STestPacket { RandomNumber = i }, PacketFlags.None);
            await Task.Delay(10);
        }

        await Task.Delay(2000);

        Assert.That(receivedCount + ClientManager.BidirectionalPacketAddResult, Is.EqualTo(0));
    }

    [Test]
    [Order(8)]
    public async Task SignOutTest()
    {
        Assert.That(ClientManager.ConnectedClient, Is.EqualTo(1));

        await Client.SendAsync(new SignOutMessage { Id = 1919870 }, PacketFlags.None);

        await Task.Delay(2000);

        Assert.Multiple(() =>
        {
            Assert.That(ClientManager.SignOutMessageVal, Is.EqualTo(1919870));
            Assert.That(ClientManager.ConnectedClient, Is.EqualTo(0));
        });
    }
}