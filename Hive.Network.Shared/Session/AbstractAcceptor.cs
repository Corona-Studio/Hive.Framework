using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hive.Common.Shared;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Shared.Session
{
    /// <summary>
    ///     链接接收器抽象
    /// </summary>
    public abstract class AbstractAcceptor<TSession> : IAcceptor<TSession> where TSession : ISession
    {
        private readonly ConcurrentDictionary<SessionId, TSession> _idToSessionDict = new();
        private readonly List<TSession> _sessionsToClose = new();
        protected readonly ILogger<AbstractAcceptor<TSession>> Logger;
        protected readonly IServiceProvider ServiceProvider;

        private int _curUsedSessionId = int.MinValue;

        protected AbstractAcceptor(IServiceProvider serviceProvider, ILogger<AbstractAcceptor<TSession>> logger)
        {
            ServiceProvider = serviceProvider;
            Logger = logger;
        }

        public abstract IPEndPoint? EndPoint { get; }

        public virtual bool IsValid { get; }
        public bool IsSelfRunning { get; protected set; }

        public event Func<TSession, ValueTask>? OnSessionCreateAsync;
        public event EventHandler<OnClientCreatedArgs<TSession>>? OnSessionCreated;
        public event EventHandler<OnClientClosedArgs<TSession>>? OnSessionClosed;
        public abstract Task<bool> SetupAsync(IPEndPoint listenEndPoint, CancellationToken token);
        
        private EventHandler<OnClientCreatedArgs<ISession>>? _onSessionCreated;
        event EventHandler<OnClientCreatedArgs<ISession>>? IAcceptor.OnSessionCreated
        {
            add => _onSessionCreated += value;
            remove => _onSessionCreated -= value;
        }
        
        private EventHandler<OnClientClosedArgs<ISession>>? _onSessionClosed;
        event EventHandler<OnClientClosedArgs<ISession>>? IAcceptor.OnSessionClosed
        {
            add => _onSessionClosed += value;
            remove => _onSessionClosed -= value;
        }
        
        // todo use async event
        private Func<ISession, ValueTask>? _onSessionCreateAsync;
        event Func<ISession, ValueTask>? IAcceptor.OnSessionCreateAsync
        {
            add => _onSessionCreateAsync += value;
            remove => _onSessionCreateAsync -= value;
        }

        ISession? IAcceptor.GetSession(SessionId sessionId)
        {
            return GetSession(sessionId);
        }


        public virtual async void StartAcceptLoop(CancellationToken token)
        {
            try
            {
                await Task.Run(async () =>
                {
                    IsSelfRunning = true;
                    while (!token.IsCancellationRequested) await DoOnceAcceptAsync(token);
                    IsSelfRunning = false;
                }, token);
            }
            catch (TaskCanceledException e)
            {
                Logger.LogInformation(e, "Accept loop canceled");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Accept loop error");
            }
        }

        public abstract Task<bool> CloseAsync(CancellationToken token);

        public abstract ValueTask<bool> DoOnceAcceptAsync(CancellationToken token);


        public TSession? GetSession(SessionId sessionId)
        {
            return _idToSessionDict.TryGetValue(sessionId, out var session) ? session : default;
        }

        public ValueTask<bool> SendToAsync(SessionId sessionId, MemoryStream buffer, CancellationToken token = default)
        {
            var session = GetSession(sessionId);
            if (session != null) return session.SendAsync(buffer, token);
            return new ValueTask<bool>(false);
        }

        public virtual void DoHeartBeatCheck()
        {
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            foreach (var (sessionId, session) in _idToSessionDict)
                if (session.LastHeartBeatTime + NetworkSettings.MaxHeartBeatTimeout < now)
                {
                    Logger.LogDebug("Session {sessionId} heartbeat timeout.", sessionId);
                    session.Close();
                    _sessionsToClose.Add(session);
                }

            foreach (var session in _sessionsToClose) _idToSessionDict.TryRemove(session.Id, out _);
            _sessionsToClose.Clear();
        }

        public abstract void Dispose();

        protected void FireOnSessionCreate(TSession session)
        {
            Logger.LogInformation("Session {sessionId} accepted.", session.Id);
            _idToSessionDict.TryAdd(session.Id, session);

            OnSessionCreated?.Invoke(this, new OnClientCreatedArgs<TSession>(session.Id, session));
            _onSessionCreated?.Invoke(this, new OnClientCreatedArgs<ISession>(session.Id, session));
            
            if (OnSessionCreateAsync != null)
                try
                {
                    OnSessionCreateAsync(session);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "OnSessionCreateAsync error");
                }
            
            if (_onSessionCreateAsync != null)
                try
                {
                    _onSessionCreateAsync(session);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "OnSessionCreateAsync error");
                }
        }

        protected void FireOnSessionClosed(TSession session)
        {
            Logger.LogInformation("Session {SessionId} closed", session.Id);
            _idToSessionDict.TryRemove(session.Id, out _);
            
            OnSessionClosed?.Invoke(this, new OnClientClosedArgs<TSession>(session.Id, session));
            _onSessionClosed?.Invoke(this, new OnClientClosedArgs<ISession>(session.Id, session));
        }

        protected int GetNextSessionId()
        {
            Interlocked.Increment(ref _curUsedSessionId);
            return _curUsedSessionId;
        }
    }
}