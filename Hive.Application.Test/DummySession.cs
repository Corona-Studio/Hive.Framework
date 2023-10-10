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
    public event SessionReceivedHandler? OnMessageReceived;

    public Task StartAsync(CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public event Action<MemoryStream>? OnSend; 
    public ValueTask<bool> SendAsync(MemoryStream ms, CancellationToken token = default)
    {
        OnSend?.Invoke(ms);
        return new ValueTask<bool>(true);
    }

    public void Close()
    {
        throw new NotImplementedException();
    }
}