using Hive.Network.Abstractions.Session;

namespace Hive.Network.Abstractions.EventArgs;

/// <summary>
/// 客户端连接状态改变事件参数
/// </summary>
/// <typeparam name="TSession">会话类型，通常为对应的协议会话</typeparam>
public class ClientConnectionChangedEventArgs<TSession> : System.EventArgs where TSession : ISession
{
    public TSession Session { get; }
    public ClientConnectionStatus Status { get; }

    public ClientConnectionChangedEventArgs(TSession session, ClientConnectionStatus status)
    {
        Session = session;
        Status = status;
    }
}