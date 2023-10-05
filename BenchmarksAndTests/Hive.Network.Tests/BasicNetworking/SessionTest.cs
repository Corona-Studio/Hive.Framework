using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Hive.Codec.MemoryPack;
using Hive.Framework.Networking.Quic;
using Hive.Framework.Shared.Helpers;
using Hive.Network.Abstractions.Session;
using Hive.Network.Kcp;
using Hive.Network.Quic;
using Hive.Network.Shared;
using Hive.Network.Tcp;
using Hive.Network.Udp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;

namespace Hive.Network.Tests.BasicNetworking;

public class SessionTestUdp : SessionTest<UdpSession>
{
    public SessionTestUdp()
    {
        LossRate = 0.95;
    }

    protected override IServiceProvider GetServiceProvider()
    {
        return ServiceProviderHelper.GetServiceProvider<UdpSession, UdpAcceptor, UdpConnector, MemoryPackPacketCodec>();
    }
}

public class SessionTestKcp : SessionTest<KcpSession>
{
    public SessionTestKcp()
    {
        LossRate = 0.95;
    }

    protected override IServiceProvider GetServiceProvider()
    {
        return ServiceProviderHelper.GetServiceProvider<KcpSession, KcpAcceptor, KcpConnector, MemoryPackPacketCodec>();
    }
}

public class SessionTestTcp : SessionTest<TcpSession>
{
    protected override IServiceProvider GetServiceProvider()
    {
        return ServiceProviderHelper.GetServiceProvider<TcpSession, TcpAcceptor, TcpConnector, MemoryPackPacketCodec>();
    }
}

[RequiresPreviewFeatures]
[SupportedOSPlatform(nameof(OSPlatform.Windows))]
[SupportedOSPlatform(nameof(OSPlatform.Linux))]
[SupportedOSPlatform(nameof(OSPlatform.OSX))]
public class SessionTestQuic : SessionTest<QuicSession>
{
    [RequiresPreviewFeatures]
    protected override IServiceProvider GetServiceProvider()
    {
        var serviceProvider = ServiceProviderHelper.GetServiceProvider<QuicSession, QuicAcceptor, QuicConnector, MemoryPackPacketCodec>(
            setter =>
            {
                setter.Configure<QuicAcceptorOptions>(options =>
                {
                    options.QuicListenerOptions = new QuicListenerOptions
                    {
                        ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                        ListenEndPoint = QuicNetworkSettings.FallBackEndPoint,
                        ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
                        {
                            DefaultStreamErrorCode = 0,
                            DefaultCloseErrorCode = 0,
                            IdleTimeout = TimeSpan.FromMinutes(5),
                            ServerAuthenticationOptions = new SslServerAuthenticationOptions
                            {
                                ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3, SslApplicationProtocol.Http2 },
                                ServerCertificate = QuicCertHelper.GenerateTestCertificate()
                            }
                        })
                    };
                });

                setter.Configure<QuicConnectorOptions>(options =>
                {
                    options.ClientConnectionOptions = new QuicClientConnectionOptions
                    {
                        RemoteEndPoint = QuicNetworkSettings.FallBackEndPoint,
                        DefaultStreamErrorCode = 0,
                        DefaultCloseErrorCode = 0,
                        IdleTimeout = TimeSpan.FromMinutes(5),
                        ClientAuthenticationOptions = new SslClientAuthenticationOptions
                        {
                            ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3, SslApplicationProtocol.Http2 },
                            RemoteCertificateValidationCallback = (_, _, _, _) => true
                        }
                    };
                });
            });

        return serviceProvider;
    }
}

public abstract class SessionTest<T> where T : class, ISession
{
    protected abstract IServiceProvider GetServiceProvider();
    private CancellationTokenSource _cts = null!;
    private IServiceProvider _serviceProvider = null!;
    private IAcceptor<T> _acceptor = null!;
    private readonly List<T> _clientSideSessions = new();
    private readonly List<T> _serverSideSessions = new();

    protected const int SendInterval = 0;
    protected double LossRate = 1;
    
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
    [Description("测试会话创建，会话数量为 5000 个")]
    public async Task TestSessionCreate()
    {
        const int randomClientNum = 5000;

        _acceptor = _serviceProvider.GetRequiredService<IAcceptor<T>>();

        var sessionCount = 0;
        var tcs = new TaskCompletionSource<bool>();
        
        _cts.Token.Register(() =>
        {
            if(tcs.Task.Status is TaskStatus.Running or TaskStatus.Canceled)
                tcs.SetCanceled(_cts.Token);
        });
        
        _acceptor.OnSessionCreated += (_, args) =>
        {
            sessionCount++;
            _serverSideSessions.Add(args.Session);
            
            if(sessionCount == randomClientNum)
                tcs.SetResult(true);
        };
        const int port = 11451;
        await _acceptor.SetupAsync(new IPEndPoint(IPAddress.Any, port),_cts.Token);
        _acceptor.StartAcceptLoop(_cts.Token);

        for (var i = 0; i < randomClientNum; i++)
        {
            var connector = _serviceProvider.GetRequiredService<IConnector<T>>();
            var session = await connector.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port),
                _cts.Token);
            if (session != null)
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
        var len = Random.Shared.Next(512,1024);
        var sb = new StringBuilder();

        for (var i = 0; i < len; i++)
        {
            //random
            sb.Append((char)Random.Shared.Next(32, 126));
        }

        return sb.ToString();
    }

    [Test]
    public async Task TestSessionSendAndReceive()
    {
        var clientSentText = new Dictionary<int, string>();
        var serverSentText = new Dictionary<int, string>();

        var c2sCorrectCount = 0;
        var s2cCorrectCount = 0;

        foreach (var session in _serverSideSessions)
        {
            session.OnMessageReceived += (_, mem) =>
            {
                var text = Encoding.UTF8.GetString(mem.Span);
                if(clientSentText.TryGetValue(session.RemoteEndPoint.Port, out var sentText))
                {
                    if(sentText == text)
                        Interlocked.Increment(ref c2sCorrectCount);
                }
            };
            session.StartAsync(_cts.Token).CatchException();
        }

        foreach (var session in _clientSideSessions)
        {
            session.OnMessageReceived += (_, mem) =>
            {
                var text = Encoding.UTF8.GetString(mem.Span);
                if(serverSentText.TryGetValue(session.LocalEndPoint.Port,out var sentText))
                {
                    if(sentText==text)
                        Interlocked.Increment(ref s2cCorrectCount);
                }
            };
            session.StartAsync(_cts.Token).CatchException();
        }
        
        foreach (var session in _clientSideSessions)
        {
            var text = GenerateLargeText();
            clientSentText.Add(session.LocalEndPoint.Port, text);
            
            var ms = RecycleMemoryStreamManagerHolder.Shared.GetStream();
            Encoding.UTF8.GetBytes(text, (RecyclableMemoryStream)ms);
            
            if (SendInterval > 0)
                await Task.Delay(SendInterval);// 防止UDP丢包

            await session.SendAsync(ms);
        }

        foreach (var session in _serverSideSessions)
        {
            var text = GenerateLargeText();
            serverSentText.Add(session.RemoteEndPoint.Port, text);
            
            var ms = RecycleMemoryStreamManagerHolder.Shared.GetStream();
            Encoding.UTF8.GetBytes(text,((RecyclableMemoryStream)ms));

            if (SendInterval>0)
                await Task.Delay(SendInterval);// 防止UDP丢包
            await session.SendAsync(ms);
        }
        
        await Task.Delay(3000);
        
        Assert.Multiple(() =>
        {
            Assert.That(c2sCorrectCount, Is.GreaterThanOrEqualTo(_clientSideSessions.Count * LossRate));
            Assert.That(s2cCorrectCount, Is.GreaterThanOrEqualTo(_serverSideSessions.Count * LossRate));
        });
    }
}