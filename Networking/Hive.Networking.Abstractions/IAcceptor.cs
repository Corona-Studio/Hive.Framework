using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions;


public interface IAcceptor
{
    
}

/// <summary>
/// 代表一个接入连接接收器
/// </summary>
/// <typeparam name="TSession">分包发送者，通常为对应的 Session</typeparam>
/// <typeparam name="TSessionId">会话 Id 的类型，用于客户端管理器</typeparam>
public interface IAcceptor<TSession, TSessionId> : IAcceptor
    where TSession : ISession<TSession>
    where TSessionId : unmanaged
{
    IDataDispatcher<TSession> DataDispatcher { get; }

    IClientManager<TSessionId, TSession> ClientManager { get; }
    
    public Task<bool> SetupAsync(CancellationToken token);
    
    public Task StartAcceptLoop(CancellationToken token);
    
    public Task<bool> CloseAsync(CancellationToken token);
    
    
    public ValueTask<bool> DoAcceptAsync(CancellationToken token);
    
    public bool IsValid { get; }
    
    /// <summary>
    /// True if the acceptor is running in a separate thread and managed by itself.
    /// </summary>
    public bool IsSelfRunning { get; }
}