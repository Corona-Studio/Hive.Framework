using Hive.Application.Test.TestMessage;
using Hive.Both.General.Dispatchers;
using Hive.Codec.Abstractions;
using Hive.Codec.MemoryPack;
using Hive.Network.Abstractions.Session;
using Hive.Network.Tcp;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Hive.Application.Test;

public class DispatcherTest
{
    [Test]
    public async Task TestDispatchAndRegister()
    {
        var serviceProvider = ServiceProviderHelper
            .GetServiceProvider<TcpSession, TcpAcceptor, TcpConnector, MemoryPackPacketCodec>();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
        var dummySession = new DummySession();


        var originMessage = new ComplexMessage();

        using var ms = new MemoryStream();
        var codec = serviceProvider.GetRequiredService<IPacketCodec>();
        codec.Encode(originMessage, ms);
        var mem = ms.GetBuffer().AsMemory()[..(int)ms.Length];
        ComplexMessage? receivedMessage = null;

        var cnt = 0;
        dispatcher.Dispatch(dummySession, mem);
        dispatcher.AddHandler<ComplexMessage>(Dispatcher);
        dispatcher.Dispatch(dummySession, mem);
        dispatcher.Dispatch(dummySession, mem);
        dispatcher.Dispatch(dummySession, mem);


        void Dispatcher(MessageContext<ComplexMessage> complexMessage)
        {
            cnt++;
        }

        Assert.That(cnt, Is.EqualTo(3));
    }

    [Test]
    public void TestRemoveHandler()
    {
        var serviceProvider = ServiceProviderHelper
            .GetServiceProvider<TcpSession, TcpAcceptor, TcpConnector, MemoryPackPacketCodec>();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
        var dummySession = new DummySession();


        var originMessage = new ComplexMessage();

        using var ms = new MemoryStream();
        var codec = serviceProvider.GetRequiredService<IPacketCodec>();
        codec.Encode(originMessage, ms);
        var mem = ms.GetBuffer().AsMemory().Slice(0, (int)ms.Length);
        ComplexMessage? receivedMessage = null;

        var cnt = 0;

        // Test remove by handler
        dispatcher.AddHandler<ComplexMessage>(Dispatcher);
        dispatcher.Dispatch(dummySession, mem);
        dispatcher.RemoveHandler<ComplexMessage>(Dispatcher);
        dispatcher.Dispatch(dummySession, mem);

        void Dispatcher(MessageContext<ComplexMessage> complexMessage)
        {
            cnt++;
        }

        Assert.That(cnt, Is.EqualTo(1));

        // Test remove by id
        var handlerId = dispatcher.AddHandler<ComplexMessage>(Dispatcher);
        dispatcher.Dispatch(dummySession, mem);
        dispatcher.RemoveHandler(handlerId);
        dispatcher.Dispatch(dummySession, mem);

        Assert.That(cnt, Is.EqualTo(2));
    }

    [Test]
    public async Task TestHandleOnce()
    {
        var serviceProvider = ServiceProviderHelper
            .GetServiceProvider<TcpSession, TcpAcceptor, TcpConnector, MemoryPackPacketCodec>();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
        var dummySession = new DummySession();

        var originMessage = new ComplexMessage();
        using var ms = new MemoryStream();
        var codec = serviceProvider.GetRequiredService<IPacketCodec>();
        codec.Encode(originMessage, ms);
        var mem = ms.GetBuffer().AsMemory().Slice(0, (int)ms.Length);
        ComplexMessage? receivedMessage = null;

        var cnt = 0;

        DelaySendMessage(dispatcher, dummySession, mem, 100);
        CancellationTokenSource cts = new();
        cts.CancelAfter(1000);
        
        var received = await dispatcher.HandleOnce<ComplexMessage>(dummySession, cts.Token);
        Assert.That(received != null);

        var receivedSecond = await dispatcher.HandleOnce<ComplexMessage>(dummySession, cts.Token);
        Assert.That(receivedSecond == null);
    }

    private async void DelaySendMessage(IDispatcher dispatcher, ISession session, Memory<byte> message,
        int delay)
    {
        try
        {
            await Task.Delay(delay);
            dispatcher.Dispatch(session, message);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}