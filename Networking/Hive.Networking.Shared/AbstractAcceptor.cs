using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Networking.Abstractions;

namespace Hive.Framework.Networking.Shared;

/// <summary>
/// 链接接收器抽象
/// </summary>
public abstract class AbstractAcceptor<TSession> : IAcceptor<TSession>, IDisposable where TSession : ISession
{
    protected AbstractAcceptor(IPEndPoint endPoint, IServiceProvider serviceProvider)
    {
        EndPoint = endPoint;
        ServiceProvider = serviceProvider;
    }

    public IPEndPoint EndPoint { get; }

    public virtual bool IsValid { get; }
    public bool IsSelfRunning { get; protected set; }
    
    public event Func<TSession, ValueTask>? OnSessionAccepted;
    
    private int _curUsedSessionId = 0;
    protected IServiceProvider ServiceProvider;

    protected async ValueTask FireOnSessionAccepted(TSession session)
    {
        if (OnSessionAccepted != null)
        {
            await OnSessionAccepted.Invoke(session);
        }
    }
    
    public abstract Task<bool> SetupAsync(CancellationToken token);

    
    public virtual Task StartAcceptLoop(CancellationToken token)
    {
        return Task.Run(async () =>
        {
            IsSelfRunning = true;
            while (!token.IsCancellationRequested)
            {
                await DoAcceptAsync(token);
            }
            IsSelfRunning = false;
        }, token);
    }


    public abstract Task<bool> CloseAsync(CancellationToken token);

    public abstract ValueTask<bool> DoAcceptAsync(CancellationToken token);
    public abstract void Dispose();
    
    protected int GetNextSessionId()
    {
        Interlocked.Increment(ref _curUsedSessionId);
        return _curUsedSessionId;
    }
}