using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions;


/// <summary>
/// 代表一个接入连接接收器
/// </summary>
public interface IAcceptor<out TSession> where TSession : ISession
{
    event Func<TSession,ValueTask> OnSessionAccepted;
    
    Task<bool> SetupAsync(CancellationToken token);
    
    Task StartAcceptLoop(CancellationToken token);
    
    Task<bool> CloseAsync(CancellationToken token);
    
    
    ValueTask<bool> DoAcceptAsync(CancellationToken token);
    
    bool IsValid { get; }
    
    /// <summary>
    /// True if the acceptor is running in a separate thread and managed by itself.
    /// </summary>
    bool IsSelfRunning { get; }
}