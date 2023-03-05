using System;
using Hive.Framework.Networking.Abstractions.EventArgs;

namespace Hive.Framework.Networking.Abstractions;

public interface IClientManager<TSessionId, TSession> where TSession : ISession<TSession>
{
    /// <summary>
    /// 根据会话获取编码后的会话 ID
    /// <para>这个方法通常被用于在网关服务器向具体服务器转发时注入的额外信息</para>
    /// </summary>
    /// <param name="session"></param>
    /// <returns>编码后的会话 ID</returns>
    ReadOnlyMemory<byte> GetEncodedSessionId(TSession session);

    /// <summary>
    /// 根据会话获取会话 ID
    /// </summary>
    /// <param name="session"></param>
    /// <returns></returns>
    TSessionId? GetSessionId(TSession session);

    /// <summary>
    /// 添加一个会话
    /// </summary>
    /// <param name="session"></param>
    void AddSession(TSession session);

    /// <summary>
    /// 更新一个会话（使用现有 ID 替换旧的会话）
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="session"></param>
    void UpdateSession(TSessionId sessionId, TSession session);

    /// <summary>
    /// 启动客户端连接检查器
    /// </summary>
    void StartClientConnectionHolder();

    /// <summary>
    /// 停止客户端连接检查器
    /// </summary>
    void StopClientConnectionHolder();

    event EventHandler<ClientConnectionChangedEventArgs<TSession>>? OnClientConnected;
    event EventHandler<ClientConnectionChangedEventArgs<TSession>>? OnClientDisconnected;
}