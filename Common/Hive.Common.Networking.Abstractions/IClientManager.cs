using System;
using System.Net;
using Hive.Framework.Networking.Abstractions.EventArgs;

namespace Hive.Framework.Networking.Abstractions;

public interface IClientManager<TSessionId, TSession>
    where TSession : ISession<TSession>
    where TSessionId : unmanaged
{
    int SessionIdSize { get; }

    /// <summary>
    /// 根据会话获取编码后的 C2S（Client -> Server） 前缀
    /// <para>这个方法通常被用于在网关服务器向具体服务器转发时注入的额外信息</para>
    /// </summary>
    /// <param name="session"></param>
    /// <returns>编码后的会话前缀</returns>
    ReadOnlyMemory<byte> GetEncodedC2SSessionPrefix(TSession session);

    /// <summary>
    /// 从完整的封包中提取并解析会话 ID
    /// </summary>
    /// <param name="payload"></param>
    /// <returns></returns>
    TSessionId ResolveSessionPrefix(ReadOnlyMemory<byte> payload);

    /// <summary>
    /// 根据会话获取会话 ID
    /// </summary>
    /// <param name="session"></param>
    /// <returns></returns>
    TSessionId GetSessionId(TSession session);

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

    /// <summary>
    /// 使用远程终结点获取会话
    /// </summary>
    /// <param name="remoteEndPoint"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    bool TryGetSession(IPEndPoint remoteEndPoint, out TSession? session);

    /// <summary>
    /// 使用客户端 ID 获取会话
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    bool TryGetSession(TSessionId sessionId, out TSession? session);

    event EventHandler<ClientConnectionChangedEventArgs<TSession>>? OnClientConnected;
    event EventHandler<ClientConnectionChangedEventArgs<TSession>>? OnClientDisconnected;
}