namespace Hive.Framework.Networking.Abstractions;

public interface IClientManager<TSessionId, in TSession>
{
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
}