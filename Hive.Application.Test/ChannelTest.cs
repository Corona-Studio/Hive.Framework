using Hive.Application.Test.TestMessage;
using Hive.Both.General;
using Hive.Both.General.Channels;
using Hive.Both.General.Dispatchers;
using Hive.Codec.Abstractions;
using Hive.Codec.MemoryPack;
using Hive.Network.Abstractions.Session;
using Hive.Network.Tcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Hive.Application.Test;

public class ChannelTest
{
    [Test]
    public async Task TestChannel()
    {
        var serviceProvider = ServiceProviderHelper
            .GetServiceProvider<TcpSession, TcpAcceptor, TcpConnector, MemoryPackPacketCodec>();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
        

        var originMessage = new ComplexMessage();

        using var ms = new MemoryStream();
        var codec = serviceProvider.GetRequiredService<IPacketCodec>();
        codec.Encode(originMessage, ms);
        var mem = ms.GetBuffer().AsMemory().Slice(0, (int)ms.Length);

        var cnt = 0;
        var sent = 0;
        
        var dummySession = new DummySession();
        dummySession.OnSend += _=>
        {
            sent++;
        };
        
        dispatcher.Dispatch(dummySession, mem);

        var channel = dispatcher.CreateServerChannel<ComplexMessage, ComplexMessage>();
        using CancellationTokenSource cts = new();
        var token = cts.Token;
        var task = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var (session, message) = await channel.ReadAsync(token);
                    cnt++;
                    await channel.WriteAsync(session, message);
                }
            }
            catch (OperationCanceledException e)
            {
                //ignore
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
                throw;
            }
            
        }, cts.Token);
        
        dispatcher.Dispatch(dummySession, mem);
        dispatcher.Dispatch(dummySession, mem);
        dispatcher.Dispatch(dummySession, mem);
        
        
        cts.CancelAfter(1000);
        await task;
        
        Assert.Equals(3, cnt);
        Assert.Equals(3, sent);
    }
    
    [Test]
    public async Task TestClientChannel()
    {
        var serviceProvider = ServiceProviderHelper
            .GetServiceProvider<TcpSession, TcpAcceptor, TcpConnector, MemoryPackPacketCodec>();
        var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
        

        var originMessage = new ComplexMessage();

        using var ms = new MemoryStream();
        var codec = serviceProvider.GetRequiredService<IPacketCodec>();
        codec.Encode(originMessage, ms);
        var mem = ms.GetBuffer().AsMemory().Slice(0, (int)ms.Length);

        var cnt = 0;
        var sent = 0;
        
        var dummySession = new DummySession();
        dummySession.OnSend += _=>
        {
            sent++;
        };
        
        dispatcher.Dispatch(dummySession, mem);

        var channel = dispatcher.CreateChannel<ComplexMessage, ComplexMessage>(dummySession);
        using CancellationTokenSource cts = new();
        var token = cts.Token;
        var task = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var message = await channel.ReadAsync(token);
                    cnt++;
                    await channel.WriteAsync(message);
                }
            }
            catch (OperationCanceledException e)
            {
                //ignore
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
                throw;
            }
            
        }, cts.Token);
        
        dispatcher.Dispatch(dummySession, mem);
        dispatcher.Dispatch(dummySession, mem);
        dispatcher.Dispatch(dummySession, mem);
        
        
        cts.CancelAfter(1000);
        await task;
        
        Assert.Equals(3, cnt);
        Assert.Equals(3, sent);
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