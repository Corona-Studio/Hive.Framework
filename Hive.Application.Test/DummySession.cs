using System.Net;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;

namespace Hive.Application.Test;

public class DummySession : ISession
{
    public SessionId Id { get; }
    public IPEndPoint? LocalEndPoint { get; }
    public IPEndPoint? RemoteEndPoint { get; }
    public long LastHeartBeatTime { get; }
    public event EventHandler<ReadOnlyMemory<byte>>? OnMessageReceived;

    public Task StartAsync(CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public ValueTask<bool> SendAsync(MemoryStream ms, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public void Close()
    {
        throw new NotImplementedException();
    }
}