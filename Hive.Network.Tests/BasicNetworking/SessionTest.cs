﻿using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Hive.Codec.MemoryPack;
using Hive.Common.Shared.Helpers;
using Hive.Network.Abstractions.Session;
using Hive.Network.Kcp;
using Hive.Network.Quic;
using Hive.Network.Shared;
using Hive.Network.Tcp;
using Hive.Network.Udp;
using Microsoft.Extensions.DependencyInjection;

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

[Ignore("NEED FIX")]
[RequiresPreviewFeatures]
[SupportedOSPlatform(nameof(OSPlatform.Windows))]
[SupportedOSPlatform(nameof(OSPlatform.Linux))]
[SupportedOSPlatform(nameof(OSPlatform.OSX))]
public class SessionTestQuic : SessionTest<QuicSession>
{
    [RequiresPreviewFeatures]
    protected override IServiceProvider GetServiceProvider()
    {
        var serviceProvider = ServiceProviderHelper
            .GetServiceProvider<QuicSession, QuicAcceptor, QuicConnector, MemoryPackPacketCodec>(
                setter =>
                {
                    setter.Configure<QuicAcceptorOptions>(options =>
                    {
                        options.QuicListenerOptions = new QuicListenerOptions
                        {
                            ApplicationProtocols = [SslApplicationProtocol.Http3],
                            ListenEndPoint = QuicNetworkSettings.FallBackEndPoint,
                            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(
                                new QuicServerConnectionOptions
                                {
                                    DefaultStreamErrorCode = 0,
                                    DefaultCloseErrorCode = 0,
                                    IdleTimeout = TimeSpan.FromMinutes(5),
                                    ServerAuthenticationOptions = new SslServerAuthenticationOptions
                                    {
                                        ApplicationProtocols = [SslApplicationProtocol.Http3],
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
                                ApplicationProtocols = [SslApplicationProtocol.Http3],
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
    private const int SendInterval = 1;
    private readonly List<T> _clientSideSessions = [];
    private readonly List<T> _serverSideSessions = [];
#pragma warning disable NUnit1032
    private IAcceptor<T> _acceptor = null!;
#pragma warning restore NUnit1032
    private CancellationTokenSource _cts = null!;
    private IServiceProvider _serviceProvider = null!;
    protected double LossRate = 1;
    protected abstract IServiceProvider GetServiceProvider();

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _serviceProvider = GetServiceProvider();
        _cts = new CancellationTokenSource();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        try
        {
            _cts.Cancel();
            _cts?.Dispose();
            _acceptor?.Dispose();

            foreach (var session in _clientSideSessions)
                session.Close();

            foreach (var session in _serverSideSessions)
                session.Close();
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    [Test]
    [Author("Leon")]
    [Description("测试会话创建，会话数量为 100 个")]
    public async Task TestSessionCreate()
    {
        const int randomClientNum = 100;

        _acceptor = _serviceProvider.GetRequiredService<IAcceptor<T>>();

        var sessionCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        _cts.Token.Register(() =>
        {
            if (tcs.Task.Status is TaskStatus.Running or TaskStatus.Canceled)
                tcs.SetCanceled(_cts.Token);
        });
        
        _acceptor.OnSessionCreated += (_, _, session) =>
        {
            sessionCount++;
            _serverSideSessions.Add(session);

            if (sessionCount == randomClientNum)
                tcs.SetResult(true);
        };
        const int port = 11451;
        await _acceptor.SetupAsync(new IPEndPoint(IPAddress.Any, port), _cts.Token);

        TaskHelper.FireAndForget(() => _acceptor.StartAcceptLoop(_cts.Token));

        for (var i = 0; i < randomClientNum; i++)
        {
            var connector = _serviceProvider.GetRequiredService<IConnector<T>>();
            var session = await connector.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port),
                _cts.Token);
            if (session != null) _clientSideSessions.Add(session);
        }

        await tcs.Task;
        Assert.Multiple(() =>
        {
            Assert.That(_clientSideSessions, Has.Count.EqualTo(randomClientNum));
            Assert.That(_clientSideSessions, Has.Count.EqualTo(_serverSideSessions.Count));
            Assert.That(sessionCount, Is.EqualTo(randomClientNum));
        });
    }

    private static string GenerateLargeText()
    {
        var len = Random.Shared.Next(512, 1024);
        var sb = new StringBuilder();

        for (var i = 0; i < len; i++)
            //random
            sb.Append((char)Random.Shared.Next(32, 126));

        return sb.ToString();
    }

    [Test]
    public async Task TestSessionSendAndReceive()
    {
        var clientSentText = new ConcurrentDictionary<int, string>();
        var serverSentText = new ConcurrentDictionary<int, string>();

        // ReSharper disable once InconsistentNaming
        var c2sCorrectCount = 0;
        // ReSharper disable once InconsistentNaming
        var s2cCorrectCount = 0;

        foreach (var session in _serverSideSessions)
        {
            session.OnMessageReceived += (_, mem) =>
            {
                var text = Encoding.UTF8.GetString(mem);
                if (!clientSentText.TryGetValue(session.RemoteEndPoint!.Port, out var sentText)) return;
                if (sentText == text)
                    Interlocked.Increment(ref c2sCorrectCount);
            };
            session.StartAsync(_cts.Token).CatchException();
        }

        foreach (var session in _clientSideSessions)
        {
            session.OnMessageReceived += (_, mem) =>
            {
                var text = Encoding.UTF8.GetString(mem);
                if (!serverSentText.TryGetValue(session.LocalEndPoint!.Port, out var sentText)) return;
                if (sentText == text)
                    Interlocked.Increment(ref s2cCorrectCount);
            };
            session.StartAsync(_cts.Token).CatchException();
        }

        foreach (var session in _clientSideSessions)
        {
            var text = GenerateLargeText();
            clientSentText.TryAdd(session.LocalEndPoint!.Port, text);

            var ms = RecycleMemoryStreamManagerHolder.Shared.GetStream();
            Encoding.UTF8.GetBytes(text, ms);

            if (SendInterval > 0)
                await Task.Delay(SendInterval); // 防止UDP丢包

            await session.TrySendAsync(ms);
        }

        foreach (var session in _serverSideSessions)
        {
            var text = GenerateLargeText();
            serverSentText.TryAdd(session.RemoteEndPoint!.Port, text);

            var ms = RecycleMemoryStreamManagerHolder.Shared.GetStream();
            Encoding.UTF8.GetBytes(text, ms);

            if (SendInterval > 0)
                await Task.Delay(SendInterval); // 防止UDP丢包

            await session.TrySendAsync(ms);
        }

        await Task.Delay(1000);

        Assert.Multiple(() =>
        {
            Assert.That(c2sCorrectCount, Is.GreaterThanOrEqualTo(_clientSideSessions.Count * LossRate));
            Assert.That(s2cCorrectCount, Is.GreaterThanOrEqualTo(_serverSideSessions.Count * LossRate));
        });
    }
}