using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Abstractions.EventArgs;
using Hive.Framework.Networking.Shared.Helpers;

namespace Hive.Framework.Networking.Shared;

/// <summary>
/// 客户端管理器抽象
/// </summary>
/// <typeparam name="TSessionId">会话 ID 类型</typeparam>
/// <typeparam name="TSession">会话类型，通常为具体的协议实现</typeparam>
public abstract class AbstractClientManager<TSessionId, TSession> : IClientManager<TSessionId, TSession>
    where TSessionId : unmanaged
    where TSession : ISession<TSession>
{
    private readonly CancellationTokenSource _clientConnectionHolderCancellationTokenSource = new();
    private readonly ConcurrentDictionary<TSessionId, TSession> _idSessionMapper = new ();
    private readonly ConcurrentDictionary<TSession, TSessionId> _sessionIdMapper = new ();
    private readonly ConcurrentDictionary<IPEndPoint, TSession> _endPointSessionMapper = new ();
    private readonly ConcurrentDictionary<TSessionId, DateTime> _lastHeartBeatReceiveTimeDic = new();
    private readonly HashSet<TSession> _disconnectedSessions = new ();

    private bool _isClientLinkHolderRunning;

    public abstract int SessionIdSize { get; }

    public event EventHandler<ClientConnectionChangedEventArgs<TSession>>? OnClientConnected;
    public event EventHandler<ClientConnectionChangedEventArgs<TSession>>? OnClientDisconnected;

    public TSessionId GetSessionId(TSession session)
    {
        return _sessionIdMapper.TryGetValue(session, out var id) ? id : default;
    }

    /// <summary>
    /// 在会话成功建立连接后添加到客户端管理器，
    /// 默认实现会在此注入：
    /// <para><see cref="RegisterHeartBeatMessage"/></para>
    /// <para><see cref="RegisterClientSignOutMessage"/></para>
    /// <para><see cref="RegisterSigninMessage"/></para>
    /// <para><see cref="RegisterReconnectMessage"/></para>
    /// </summary>
    /// <param name="session">客户端会话</param>
    public virtual void AddSession(TSession session)
    {
        RegisterHeartBeatMessage(session);
        RegisterSigninMessage(session);
        RegisterClientSignOutMessage(session);
        RegisterReconnectMessage(session);

        if (_isClientLinkHolderRunning) return;

        StartClientConnectionHolder();

        _isClientLinkHolderRunning = true;
    }

    public void UpdateSession(TSessionId sessionId, TSession session)
    {
        if (!_idSessionMapper.TryGetValue(sessionId, out var oldSession))
            throw new InvalidOperationException("未在客户端记录中找到会话 ID，可能是内部错误");

        _idSessionMapper.TryUpdate(sessionId, session, oldSession);

        _sessionIdMapper.TryRemove(oldSession, out _);
        _sessionIdMapper.AddOrUpdate(session, sessionId, (_, _) => sessionId);
        
        _endPointSessionMapper.TryRemove(oldSession.RemoteEndPoint, out _);
        _endPointSessionMapper.AddOrUpdate(session.RemoteEndPoint, session, (_, _) => session);
    }

    /// <summary>
    /// 启动客户端链接保持器（定时发送心跳包查活）
    /// <para>默认实现会自动启动任务 <see cref="ClientConnectionHolderLoop"/> 来开始查活进程</para>
    /// </summary>
    public virtual void StartClientConnectionHolder()
    {
        TaskHelper.ManagedRun(
            () => ClientConnectionHolderLoop(_clientConnectionHolderCancellationTokenSource.Token),
            _clientConnectionHolderCancellationTokenSource.Token);
    }

    public virtual void StopClientConnectionHolder()
    {
        _clientConnectionHolderCancellationTokenSource.Cancel();
    }
    
    public abstract ReadOnlyMemory<byte> GetEncodedC2SSessionPrefix(TSession session);
    public abstract TSessionId ResolveSessionPrefix(ReadOnlyMemory<byte> payload);

    protected abstract void RegisterHeartBeatMessage(TSession session);
    protected abstract void RegisterSigninMessage(TSession session);
    protected abstract void RegisterClientSignOutMessage(TSession session);
    protected abstract void RegisterReconnectMessage(TSession session);

    /// <summary>
    /// 更新接收到心跳包的实现
    /// <para>您应该在 <see cref="RegisterHeartBeatMessage"/> 的事件处理程序中调用该方法来更新查活事件</para>
    /// </summary>
    /// <param name="sessionId"></param>
    protected virtual void UpdateHeartBeatReceiveTime(TSessionId sessionId)
    {
        _lastHeartBeatReceiveTimeDic.AddOrUpdate(sessionId, DateTime.Now, (_, _) => DateTime.Now);
    }

    /// <summary>
    /// 客户端链接保持器实现
    /// <para>默认实现会轮询所有已注册的客户端并向他们发送心跳包，接收心跳的处理应当在 <see cref="RegisterHeartBeatMessage"/> 事件中实现</para>
    /// <para>同时，默认实现还会主动移除查活失败的客户端会话。</para>
    /// <para>查活失败的客户端将会调用 <see cref="OnClientDisconnected"/> 来完成后续的清理操作</para>
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    protected virtual async Task ClientConnectionHolderLoop(CancellationToken cancellationToken)
    {
        var receiveTimeNotFoundCounter = new Dictionary<TSessionId, int>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var clientIds = _idSessionMapper.Keys;

            foreach (var id in clientIds)
            {
                if (!_idSessionMapper.TryGetValue(id, out var session)) continue;

                // 客户端有可能一直不进行应答，因此查活记录里面可能会没有相应的 ID，在此多加入一层检测
                if (!_lastHeartBeatReceiveTimeDic.TryGetValue(id, out var receiveTime))
                {
                    if (!receiveTimeNotFoundCounter.TryGetValue(id, out var count))
                    {
                        receiveTimeNotFoundCounter[id] = 0;
                        count = 0;
                    }
                    else
                    {
                        receiveTimeNotFoundCounter[id]++;
                        count++;
                    }

                    // 如果客户端超过五次都查活失败，则强制下线客户端
                    if (count > 5)
                    {
                        // 移除查活记录
                        _lastHeartBeatReceiveTimeDic.TryRemove(id, out _);
                        receiveTimeNotFoundCounter.Remove(id);

                        InvokeOnClientDisconnected(id, session, false);
                        continue;
                    }
                }

                // 如果客户端超过 30 秒都未回应查活请求，则强制下线
                if (DateTime.Now - receiveTime > TimeSpan.FromSeconds(30))
                {
                    // 移除查活记录
                    _lastHeartBeatReceiveTimeDic.TryRemove(id, out _);
                    receiveTimeNotFoundCounter.Remove(id);

                    InvokeOnClientDisconnected(id, session, false);
                    continue;
                }

                // 重置未收到心跳包的记录
                receiveTimeNotFoundCounter.Remove(id);
            }

            await Task.Delay(5000, cancellationToken);
        }
    }

    /// <summary>
    /// 使用远程终结点获取会话
    /// </summary>
    /// <param name="remoteEndPoint"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    public virtual bool TryGetSession(IPEndPoint remoteEndPoint, out TSession? session)
    {
        var connectedSessionResult = _endPointSessionMapper.TryGetValue(remoteEndPoint, out session);

        if (connectedSessionResult) return connectedSessionResult;

        TSession? disconnectedSession;

        lock (_disconnectedSessions)
        {
            disconnectedSession =
                _disconnectedSessions.FirstOrDefault(s => s.RemoteEndPoint.Equals(remoteEndPoint));

            if (disconnectedSession != null && disconnectedSession.ShouldDestroyAfterDisconnected)
            {
                session = default;
                return false;
            }

            session = disconnectedSession;
        }

        return disconnectedSession != null;
    }

    public virtual bool TryGetSession(TSessionId sessionId, out TSession? session)
    {
        return _idSessionMapper.TryGetValue(sessionId, out session);
    }

    /// <summary>
    /// 当客户端成功连接时调用，
    /// 默认的实现会将会话加入在线列表并开始轮询在线状态
    /// <para>如果客户端断开了连接，则调用 <see cref="OnClientDisconnected"/> 方法</para>
    /// <para>您应该在 <see cref="RegisterSigninMessage"/> 的事件处理程序中调用该方法</para>
    /// </summary>
    /// <param name="session">客户端会话</param>
    protected virtual void InvokeOnClientConnected(TSession session)
    {
        var newId = CreateNewSessionId();

        _idSessionMapper.TryAdd(newId, session);
        _sessionIdMapper.TryAdd(session, newId);
        _endPointSessionMapper.TryAdd(session.RemoteEndPoint, session);

        OnClientConnected?.Invoke(this, new ClientConnectionChangedEventArgs<TSession>(session, ClientConnectionStatus.Connected));
    }

    /// <summary>
    /// 当客户端与服务器断开连接时调用，
    /// 默认的实现会将会话从在线列表中移除并添加到已经离线的列表当中
    /// <para>您应该在 <see cref="RegisterClientSignOutMessage"/> 中调用该方法，并在调用时将 <paramref name="isClientRequest"/> 设置为 <value>true</value></para>
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="session">客户端会话</param>
    /// <param name="isClientRequest">是否是客户端主动发起的断开连接</param>
    protected virtual void InvokeOnClientDisconnected(TSessionId sessionId, TSession session, bool isClientRequest)
    {
        _idSessionMapper.TryRemove(sessionId, out _);
        _sessionIdMapper.TryRemove(session, out _);
        _endPointSessionMapper.TryRemove(session.RemoteEndPoint, out _);

        lock(_disconnectedSessions)
            _disconnectedSessions.Add(session);

        if (session.ShouldDestroyAfterDisconnected)
        {
            session.DataDispatcher.UnregisterAll();
            session.DoDisconnect();
        }

        OnClientDisconnected?.Invoke(this, new ClientConnectionChangedEventArgs<TSession>(session, ClientConnectionStatus.Disconnected));
    }

    /// <summary>
    /// 当客户端重新连接到服务器时调用，
    /// <para>默认的实现会在 <paramref name="treatAsNewClient"/> 为 <value>true</value> 时调用 <see cref="OnClientConnected"/> 事件</para>
    /// <para>在其为 <value>false</value> 时调用 <see cref="UpdateSession"/></para>
    /// <para>您应该在 <see cref="OnClientReconnected"/> 中调用该方法，并在记录可用时提供老的 <paramref name="sessionId"/></para>
    /// </summary>
    /// <param name="session">客户端会话</param>
    /// <param name="sessionId">客户端 ID，在 <paramref name="treatAsNewClient"/> 为 <value>true</value> 时该字段不为空</param>
    /// <param name="treatAsNewClient">是否将该会话当做新客户端处理</param>
    public virtual void InvokeOnClientReconnected(TSession session, TSessionId sessionId, bool treatAsNewClient)
    {
        lock (_disconnectedSessions)
            _disconnectedSessions.Remove(session);

        if (treatAsNewClient)
        {
            InvokeOnClientConnected(session);
            return;
        }

        UpdateSession(sessionId, session);
    }


    protected abstract TSessionId CreateNewSessionId();
}