using System.Net;
using System.Text;
using Hive.Codec.MemoryPack;
using Hive.Framework.Shared.Helpers;
using Hive.Network.Abstractions.Session;
using Hive.Network.Shared;
using Hive.Network.Tcp;
using Hive.Network.Udp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;

namespace Hive.Network.Tests.BasicNetworking;

public class SessionTestUDP : SessionTest<UdpSession>
{
    public SessionTestUDP()
    {
        lossRate = 0.95;
    }
    public override IServiceProvider GetServiceProvider()
    {
        return ServiceProviderHelper.GetServiceProvider<
            UdpSession, UdpAcceptor, UdpConnector, MemoryPackPacketCodec>();
    }
}

public class SessionTestTCP : SessionTest<TcpSession>
{
    public override IServiceProvider GetServiceProvider()
    {
        return ServiceProviderHelper.GetServiceProvider<
            TcpSession, TcpAcceptor, TcpConnector, MemoryPackPacketCodec>();
    }
}

public abstract class SessionTest<T> where T : class, ISession
{
    public abstract IServiceProvider GetServiceProvider();
    private CancellationTokenSource _cts;
    private IServiceProvider _serviceProvider;
    private IAcceptor<T> _acceptor;
    private readonly List<T> _clientSideSessions = new();
    private readonly List<T> _serverSideSessions = new();

    protected int sendInterval = 0;
    protected double lossRate = 1;
    
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _serviceProvider = GetServiceProvider();
        _cts = new CancellationTokenSource();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _cts.Cancel();
        _cts?.Dispose();
        _acceptor?.Dispose();
        foreach (var session in _clientSideSessions)
        {
            session.Close();
        }
        foreach (var session in _serverSideSessions)
        {
            session.Close();
        }
    }
    
    [Test]
    [Author("Leon")]
    [Description("测试会话创建，会话数量为200个")]
    public async Task TestSessionCreate()
    {
        int randomClientNum = 5000;//new Random(DateTimeOffset.Now.Millisecond).Next(2000, 10000);

        _acceptor = _serviceProvider.GetRequiredService<IAcceptor<T>>();

        var sessionCount = 0;
        var tcs = new TaskCompletionSource<bool>();
        
        _cts.Token.Register(() =>
        {
            if(tcs.Task.Status is TaskStatus.Running or TaskStatus.Canceled)
                tcs.SetCanceled(_cts.Token);
        });
        
        _acceptor.OnSessionCreated += ((sender, args) =>
        {
            sessionCount++;
            _serverSideSessions.Add(args.Session);
            
            if(sessionCount == randomClientNum)
                tcs.SetResult(true);
        });
        var port = 11451;
        await _acceptor.SetupAsync(new IPEndPoint(IPAddress.Any, port),_cts.Token);
        _acceptor.StartAcceptLoop(_cts.Token);

        for (int i = 0; i < randomClientNum; i++)
        {
            var connector = _serviceProvider.GetRequiredService<IConnector<T>>();
            var session = await connector.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port),
                _cts.Token);
            if (session!=null)
            {
                _clientSideSessions.Add(session);
            }
        }
        
        
        await tcs.Task;
        Assert.Multiple(() =>
        {
            Assert.That(_clientSideSessions, Has.Count.EqualTo(randomClientNum));
            Assert.That(_clientSideSessions, Has.Count.EqualTo(_serverSideSessions.Count));
            Assert.That(sessionCount, Is.EqualTo(randomClientNum));
        });
    }

    private string GenerateLargeText()
    {
        var random = new Random(DateTimeOffset.Now.Millisecond);
        int len = random.Next(512,1520);
        var sb = new StringBuilder();
        for (int i = 0; i < len; i++)
        {
            //random
            sb.Append((char) random.Next(1, 255));
        }

        return sb.ToString();
    }

    [Test]
    public async Task TestSessionSendAndReceive()
    {
        Dictionary<int,string> clientSentText = new();
        Dictionary<int,string> serverSentText = new();
        int c2sCorrectCount = 0;
        int s2cCorrectCount = 0;
        foreach (var session in _serverSideSessions)
        {
            session.OnMessageReceived += (sender, mem) =>
            {
                var text = Encoding.UTF8.GetString(mem.Span);
                if(clientSentText.TryGetValue(session.RemoteEndPoint.Port,out var sentText))
                {
                    if(sentText==text)
                        Interlocked.Increment(ref c2sCorrectCount);
                }
            };
            session.StartAsync(_cts.Token).CatchException(null);
        }

        foreach (var session in _clientSideSessions)
        {
            session.OnMessageReceived += (sender, mem) =>
            {
                var text = Encoding.UTF8.GetString(mem.Span);
                if(serverSentText.TryGetValue(session.LocalEndPoint.Port,out var sentText))
                {
                    if(sentText==text)
                        Interlocked.Increment(ref s2cCorrectCount);
                }
            };
            session.StartAsync(_cts.Token).CatchException(null);
        }
        
        foreach (var session in _clientSideSessions)
        {
            var text = GenerateLargeText();
            clientSentText.Add(session.LocalEndPoint.Port, text);
            
            var ms = RecycleMemoryStreamManagerHolder.Shared.GetStream();
            Encoding.UTF8.GetBytes(text,((RecyclableMemoryStream)ms));
            
            if(sendInterval>0)
                await Task.Delay(sendInterval);// 防止UDP丢包
            await session.SendAsync(ms);
        }

        foreach (var session in _serverSideSessions)
        {
            var text = GenerateLargeText();
            serverSentText.Add(session.RemoteEndPoint.Port, text);
            
            var ms = RecycleMemoryStreamManagerHolder.Shared.GetStream();
            Encoding.UTF8.GetBytes(text,((RecyclableMemoryStream)ms));

            if(sendInterval>0)
                await Task.Delay(sendInterval);// 防止UDP丢包
            await session.SendAsync(ms);
        }
        
        await Task.Delay(3000);
        
        Assert.Multiple(() =>
        {
            Assert.That(c2sCorrectCount, Is.GreaterThanOrEqualTo(_clientSideSessions.Count*lossRate));
            Assert.That(s2cCorrectCount, Is.GreaterThanOrEqualTo(_serverSideSessions.Count*lossRate));
        });
    }
}