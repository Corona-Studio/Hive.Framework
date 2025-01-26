using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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

        private int _curUsedSessionId;

        private Action<IAcceptor, SessionId, ISession>? _onSessionClosed;

        // todo use async event
        private Func<ISession, ValueTask>? _onSessionCreateAsync;

        private Action<IAcceptor, SessionId, ISession>? _onSessionCreated;

        protected AbstractAcceptor(IServiceProvider serviceProvider, ILogger<AbstractAcceptor<TSession>> logger)
        {
            ServiceProvider = serviceProvider;
            Logger = logger;
        }

        public abstract IPEndPoint? EndPoint { get; }

        public virtual bool IsValid { get; }
        public bool IsSelfRunning { get; protected set; }

        public event Func<TSession, ValueTask>? OnSessionCreateAsync;
        public event Action<IAcceptor, SessionId, TSession>? OnSessionCreated;
        public event Action<IAcceptor, SessionId, TSession>? OnSessionClosed;
        public abstract Task SetupAsync(IPEndPoint listenEndPoint, CancellationToken token);

        event Action<IAcceptor, SessionId, ISession>? IAcceptor.OnSessionCreated
        {
            add => _onSessionCreated += value;
            remove => _onSessionCreated -= value;
        }

        event Action<IAcceptor, SessionId, ISession>? IAcceptor.OnSessionClosed
        {
            add => _onSessionClosed += value;
            remove => _onSessionClosed -= value;
        }

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
                    while (!token.IsCancellationRequested) await TryDoOnceAcceptAsync(token);
                    IsSelfRunning = false;
                }, token);
            }
            catch (TaskCanceledException e)
            {
                Logger.LogAcceptLoopCanceled(e);
            }
            catch (Exception e)
            {
                Logger.LogAcceptLoopError(e);
            }
        }

        public abstract Task<bool> TryCloseAsync(CancellationToken token);

        public abstract ValueTask<bool> TryDoOnceAcceptAsync(CancellationToken token);


        public TSession? GetSession(SessionId sessionId)
        {
            return _idToSessionDict.TryGetValue(sessionId, out var session) ? session : default;
        }

        public async ValueTask<bool> TrySendToAsync(SessionId sessionId, MemoryStream buffer,
            CancellationToken token = default)
        {
            var session = GetSession(sessionId);
            if (session == null)
                return false;

            return await session.TrySendAsync(buffer, token);
        }

        public ValueTask SendToAsync(SessionId sessionId, MemoryStream buffer, CancellationToken token = default)
        {
            var session = GetSession(sessionId);
            if (session == null)
                throw new ArgumentException($"Session {sessionId} not found.");

            return session.SendAsync(buffer, token);
        }

        public virtual void DoHeartBeatCheck()
        {
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            foreach (var (sessionId, session) in _idToSessionDict)
                if (session.LastHeartBeatTime + NetworkSettings.MaxHeartBeatTimeout < now)
                {
                    Logger.LogSessionHeartBeatTimeout(sessionId);
                    session.Close();
                    _sessionsToClose.Add(session);
                }

            foreach (var session in _sessionsToClose) _idToSessionDict.TryRemove(session.Id, out _);
            _sessionsToClose.Clear();
        }

        public abstract void Dispose();

        protected void FireOnSessionCreate(TSession session)
        {
            Logger.LogSessionAccepted(session.Id);
            _idToSessionDict.TryAdd(session.Id, session);

            OnSessionCreated?.Invoke(this, session.Id, session);
            _onSessionCreated?.Invoke(this, session.Id, session);

            if (OnSessionCreateAsync != null)
                try
                {
                    OnSessionCreateAsync(session);
                }
                catch (Exception e)
                {
                    Logger.LogOnSessionCreateAsyncError(e);
                }

            if (_onSessionCreateAsync != null)
                try
                {
                    _onSessionCreateAsync(session);
                }
                catch (Exception e)
                {
                    Logger.LogOnSessionCreateAsyncError(e);
                }
        }

        protected void FireOnSessionClosed(TSession session)
        {
            Logger.LogSessionClosed(session.Id);
            _idToSessionDict.TryRemove(session.Id, out _);

            OnSessionClosed?.Invoke(this, session.Id, session);
            _onSessionClosed?.Invoke(this, session.Id, session);
        }

        protected int GetNextSessionId()
        {
            Interlocked.Increment(ref _curUsedSessionId);
            return _curUsedSessionId;
        }
    }

    internal static partial class AbstractAcceptorLoggers
    {
        [LoggerMessage(LogLevel.Warning, "Accept loop canceled")]
        public static partial void LogAcceptLoopCanceled(this ILogger logger, Exception ex);

        [LoggerMessage(LogLevel.Error, "Accept loop error")]
        public static partial void LogAcceptLoopError(this ILogger logger, Exception ex);

        [LoggerMessage(LogLevel.Debug, "Session {sessionId} heartbeat timeout.")]
        public static partial void LogSessionHeartBeatTimeout(this ILogger logger, SessionId sessionId);

        [LoggerMessage(LogLevel.Information, "Session {sessionId} accepted.")]
        public static partial void LogSessionAccepted(this ILogger logger, SessionId sessionId);

        [LoggerMessage(LogLevel.Error, "OnSessionCreateAsync error")]
        public static partial void LogOnSessionCreateAsyncError(this ILogger logger, Exception ex);

        [LoggerMessage(LogLevel.Information, "Session {SessionId} closed")]
        public static partial void LogSessionClosed(this ILogger logger, SessionId sessionId);
    }
}