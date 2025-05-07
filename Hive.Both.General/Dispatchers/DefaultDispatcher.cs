using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hive.Codec.Abstractions;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Hive.Network.Shared;
using Microsoft.Extensions.Logging;

namespace Hive.Both.General.Dispatchers
{
    public class DefaultDispatcher : IDispatcher
    {
        private readonly ConcurrentDictionary<Delegate, HandlerId> _delegateToId = new();
        private readonly ConcurrentDictionary<HandlerId, IHandleWarp> _idToTypes = new();
        private readonly ILogger<DefaultDispatcher> _logger;
        private readonly IPacketCodec _packetCodec;
        private readonly ConcurrentDictionary<Type, ConcurrentBag<HandlerId>> _typeToHandlerIds = new();

        private int _idCounter;

        public DefaultDispatcher(
            IPacketCodec packetCodec,
            ILogger<DefaultDispatcher> logger)
        {
            _packetCodec = packetCodec;
            _logger = logger;
        }

        public void Dispatch(ISession session, ReadOnlySequence<byte> buffer)
        {
            var message = _packetCodec.Decode(buffer);

            if (message == null)
            {
                _logger.LogMessageDecodeFailed();

#if DEBUG
                var base64 = Convert.ToBase64String(buffer.ToArray());
                _logger.LogRawMessage(base64);
#endif

                return;
            }

            _logger.LogMessageResolved(session.RemoteEndPoint!, message.GetType());

            Dispatch(session, message.GetType(), message);
        }

        public void Dispatch<T>(ISession session, T message) where T : class
        {
            Dispatch(session, typeof(T), message);
        }

        public void Dispatch(ISession session, Type type, object message)
        {
            if (_typeToHandlerIds.TryGetValue(type, out var handlers))
                foreach (var id in handlers)
                {
                    if (!_idToTypes.TryGetValue(id, out var warp))
                    {
                        _logger.LogHandlerIdNotFound(id);
                        continue;
                    }

                    if (warp.BindingSession != null && warp.BindingSession != session)
                        continue;

                    try
                    {
                        warp.Call(this, session, message);
                    }
                    catch (Exception e)
                    {
                        _logger.LogFailedToInvokeMessageHandler(e, message);
                    }
                }
        }

        public HandlerId AddHandler<T>(Action<MessageContext<T>> handler, TaskScheduler? scheduler = null)
        {
            var warp = new HandlerWarp<T>(GetNextId(), handler);
            AddHandler(warp);
            return warp.Id;
        }

        public bool RemoveHandler<T>(Action<MessageContext<T>> handler)
        {
            if (_delegateToId.TryRemove(handler, out var id))
                if (_idToTypes.TryRemove(id, out var warp))
                    if (_typeToHandlerIds.TryGetValue(warp.Type, out var handlers))
                    {
                        handlers.TryTake(out _);
                        return true;
                    }

            _logger.LogRemoveHandlerFailed(handler);
            return false;
        }

        public bool RemoveHandler(HandlerId id)
        {
            if (_idToTypes.TryRemove(id, out var warp))
                if (_typeToHandlerIds.TryGetValue(warp.Type, out var handlers))
                {
                    handlers.TryTake(out id);
                    if (_delegateToId.TryRemove(warp.HandlerDelegate, out _)) return true;
                }

            _logger.LogRemoveHandlerFailed(id);

            return false;
        }

        public async Task<T?> HandleOnce<T>(ISession session, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<T> tcs = new();
            var handlerWarp = new HandlerWarp<T>(GetNextId(), Handler)
            {
                BindingSession = session
            };
            AddHandler(handlerWarp);

            var id = handlerWarp.Id;

            cancellationToken.Register(() =>
            {
                _logger.LogListenOnceCanceledByToken(id);
                tcs.SetCanceled();
            });

            // todo cancel by session close

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogListenOnceCanceledBeforeListen(id);
                    return default;
                }

                var result = await tcs.Task;
                return result;
            }
            catch (TaskCanceledException)
            {
                _logger.LogListenOnceCanceled(id);
            }
            catch (Exception e)
            {
                _logger.LogListenOnceFailed(e, id);
            }
            finally
            {
                RemoveHandler(id);
                _logger.LogListenOnceRemovedHandler(id);
            }

            return default;

            void Handler(MessageContext<T> context)
            {
                var sender = context.FromSession;
                var message = context.Message;

                _logger.LogListenOnceMessageReceived(sender.Id, message);

                if (cancellationToken.IsCancellationRequested)
                {
                    if (tcs.Task.Status != TaskStatus.Canceled)
                        tcs.SetCanceled();
                }
                else
                {
                    tcs.SetResult(message);
                }
            }
        }

        public async Task<TResp?> SendAndListenOnce<TReq, TResp>(
            ISession session,
            TReq message,
            CancellationToken token = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var task = HandleOnce<TResp>(session, cts.Token);
            var sentSucceed = await SendAsync(session, message, token);

            if (!sentSucceed)
            {
                _logger.LogSendAndListenOnceCanceled(message);
                cts.Cancel();
                return default;
            }

            return await task;
        }

        public async ValueTask<bool> SendAsync<T>(ISession session, T message, CancellationToken cancellationToken = default)
        {
            await using var stream = RecycleMemoryStreamManagerHolder.Shared.GetStream();
            _packetCodec.Encode(message, stream);

            return await session.TrySendAsync(stream, cancellationToken);
        }

        private void AddHandler<T>(HandlerWarp<T> warp)
        {
            var type = warp.Type;
            var id = warp.Id;

            if (!_typeToHandlerIds.TryGetValue(type, out var handlers))
            {
                handlers = new ConcurrentBag<HandlerId>();

                if (!_typeToHandlerIds.TryAdd(type, handlers))
                    _logger.LogAddHandlerFailed(type);
            }

            handlers.Add(id);
            if (!_delegateToId.TryAdd(warp.HandlerDelegate, id))
            {
                _logger.LogAddHandlerFailed(id);
                return;
            }

            if (!_idToTypes.TryAdd(id, warp))
            {
                _logger.LogAddHandlerFailed(id);
                return;
            }

            _logger.LogAddHandlerSucceed(id, type);
        }


        private HandlerId GetNextId()
        {
            return new HandlerId(Interlocked.Increment(ref _idCounter));
        }

        public interface IHandleWarp
        {
            Type Type { get; }
            Delegate HandlerDelegate { get; }

            ISession? BindingSession { get; }
            void Call(IDispatcher dispatcher, ISession sender, object message);
        }

        public class HandlerWarp<T> : IHandleWarp
        {
            public HandlerWarp(HandlerId id, Action<MessageContext<T>> handler)
            {
                Handler = handler;
                Id = id;
            }

            public HandlerId Id { get; }
            public Action<MessageContext<T>> Handler { get; }

            public Type Type => typeof(T);
            public Delegate HandlerDelegate => Handler;

            public ISession? BindingSession { get; set; }

            public void Call(IDispatcher dispatcher, ISession sender, object message)
            {
                if (message is T t) Handler.Invoke(new MessageContext<T>(sender, dispatcher, t));
            }
        }
    }

    internal static partial class DefaultDispatcherLoggers
    {
        [LoggerMessage(LogLevel.Trace, "Decode message failed")]
        public static partial void LogMessageDecodeFailed(this ILogger logger);

        [LoggerMessage(LogLevel.Trace, "Raw message: {RawMessage}")]
        public static partial void LogRawMessage(this ILogger logger, string rawMessage);

        [LoggerMessage(LogLevel.Trace, "Message resolved from session [{endPoint}]<{type}>")]
        public static partial void LogMessageResolved(this ILogger logger, IPEndPoint endPoint, Type type);

        [LoggerMessage(LogLevel.Warning, "Handler id {HandlerId} not found")]
        public static partial void LogHandlerIdNotFound(this ILogger logger, HandlerId handlerId);

        [LoggerMessage(LogLevel.Warning, "Remove handler failed, handler:{Handler}")]
        public static partial void LogRemoveHandlerFailed(this ILogger logger, Delegate handler);

        [LoggerMessage(LogLevel.Warning, "Remove handler failed, id:{HandlerId}")]
        public static partial void LogRemoveHandlerFailed(this ILogger logger, HandlerId handlerId);

        [LoggerMessage(LogLevel.Trace, "Listen once canceled by token, handlerId:{HandlerId}")]
        public static partial void LogListenOnceCanceledByToken(this ILogger logger, HandlerId handlerId);

        [LoggerMessage(LogLevel.Trace, "Listen once canceled before listen, handlerId:{HandlerId}")]
        public static partial void LogListenOnceCanceledBeforeListen(this ILogger logger, HandlerId handlerId);

        [LoggerMessage(LogLevel.Warning, "Listen once canceled, handlerId:{HandlerId}")]
        public static partial void LogListenOnceCanceled(this ILogger logger, HandlerId handlerId);

        [LoggerMessage(LogLevel.Error, "Listen once failed, handlerId:{HandlerId}")]
        public static partial void LogListenOnceFailed(this ILogger logger, Exception ex, HandlerId handlerId);

        [LoggerMessage(LogLevel.Trace, "Listen once removed handler, handlerId:{HandlerId}")]
        public static partial void LogListenOnceRemovedHandler(this ILogger logger, HandlerId handlerId);

        [LoggerMessage(LogLevel.Trace, "Listen once received message, session:{SessionId}, message:{Message}")]
        public static partial void LogListenOnceMessageReceived(this ILogger logger, SessionId sessionId,
            object? message);

        [LoggerMessage(LogLevel.Error, "SendAndListenOnce canceled, send message failed: {Message}")]
        public static partial void LogSendAndListenOnceCanceled(this ILogger logger, object? message);

        [LoggerMessage(LogLevel.Error, "Add handler failed, type:{HandlerType}")]
        public static partial void LogAddHandlerFailed(this ILogger logger, Type handlerType);

        [LoggerMessage(LogLevel.Error, "Add handler failed, id:{HandlerId}")]
        public static partial void LogAddHandlerFailed(this ILogger logger, HandlerId handlerId);

        [LoggerMessage(LogLevel.Trace, "Add handler succeed, id: {HandlerId}, message type: {type}")]
        public static partial void LogAddHandlerSucceed(this ILogger logger, HandlerId handlerId, Type type);

        [LoggerMessage(LogLevel.Critical, "Failed to invoke handler message! {@msg}")]
        public static partial void LogFailedToInvokeMessageHandler(this ILogger logger, Exception ex, object? msg);
    }
}