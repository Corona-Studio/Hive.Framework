using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Network.Abstractions.Session;
using Hive.Network.Shared;
using Microsoft.Extensions.Logging;

namespace Hive.Both.General
{
    public class DefaultDispatcher : IDispatcher
    {
        private readonly ILogger<DefaultDispatcher> _logger;
        private readonly IPacketCodec _packetCodec;
        private readonly ConcurrentDictionary<Type, ConcurrentBag<HandlerId>> _typeToHandlerIds = new();
        private readonly ConcurrentDictionary<HandlerId, IHandleWarp> _idToTypes = new();
        private readonly ConcurrentDictionary<Delegate, HandlerId> _delegateToId = new();

        private int _idCounter;

        public DefaultDispatcher(
            IPacketCodec packetCodec,
            ILogger<DefaultDispatcher> logger)
        {
            _packetCodec = packetCodec;
            _logger = logger;
        }

        public void Dispatch(ISession session, ReadOnlyMemory<byte> rawMessage)
        {
            using var stream = RecycleMemoryStreamManagerHolder.Shared.GetStream();
            stream.Write(rawMessage.Span);
            stream.Seek(0, SeekOrigin.Begin);

            var message = _packetCodec.Decode(stream);
            if (message == null)
            {
                _logger.LogError("Decode message failed");
#if DEBUG
                var base64 = Convert.ToBase64String(rawMessage.Span);
                _logger.LogTrace("Raw message: {RawMessage}", base64);
#endif

                return;
            }

            Dispatch(session, message.GetType(), message);
        }

        public void Dispatch<T>(ISession session, T message) where T : class
        {
            Dispatch(session, typeof(T), message);
        }

        public void Dispatch(ISession session, Type type, object message)
        {
            if (_typeToHandlerIds.TryGetValue(type, out var handlers))
            {
                foreach (var id in handlers)
                {
                    if (!_idToTypes.TryGetValue(id, out var warp))
                    {
                        _logger.LogWarning("Handler id {HandlerId} not found", id);
                        continue;
                    }
                    
                    if(warp.BindingSession!=null && warp.BindingSession!=session)
                        continue;

                    warp.Call(this, session, message);
                }
            }
        }

        public HandlerId AddHandler<T>(DispatchHandler<T> handler, TaskScheduler? scheduler = null)
        {
            var warp = new HandlerWarp<T>(GetNextId(), handler);
            AddHandler(warp);
            return warp.Id;
        }
        
        private void AddHandler<T>(HandlerWarp<T> warp)
        {
            var type = warp.Type;

            var id = warp.Id;
            if (!_typeToHandlerIds.TryGetValue(type, out var handlers))
            {
                handlers = new ConcurrentBag<HandlerId>();
                if (!_typeToHandlerIds.TryAdd(type, handlers))
                {
                    _logger.LogError("Add handler failed, type:{HandlerType}", type);
                }
            }

            handlers.Add(id);
            if (!_delegateToId.TryAdd(warp.HandlerDelegate, id))
            {
                _logger.LogError("Add handler failed, id:{HandlerId}", id);
                return;
            }
            if (!_idToTypes.TryAdd(id, warp))
            {
                _logger.LogError("Add handler failed, id:{HandlerId}", id);
                return;
            }
            
            _logger.LogTrace("Add handler succeed, id:{HandlerId}, message type:{type}", id, type);
        }

        public bool RemoveHandler<T>(DispatchHandler<T> handler)
        {
            if (_delegateToId.TryRemove(handler, out var id))
            {
                if (_idToTypes.TryRemove(id, out var warp))
                {
                    if (_typeToHandlerIds.TryGetValue(warp.Type, out var handlers))
                    {
                        handlers.TryTake(out id);
                        return true;
                    }
                }
            }

            _logger.LogWarning("Remove handler failed, handler:{Handler}", handler);
            return false;
        }

        public bool RemoveHandler(HandlerId id)
        {
            if (_idToTypes.TryRemove(id, out var warp))
            {
                if (_typeToHandlerIds.TryGetValue(warp.Type, out var handlers))
                {
                    handlers.TryTake(out id);
                    if (_delegateToId.TryRemove(warp.HandlerDelegate, out _))
                    {
                        return true;
                    }
                }
            }

            _logger.LogWarning("Remove handler failed, id:{HandlerId}", id);

            return false;
        }

        public async Task<T> HandleOnce<T>(ISession session, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<T> tcs = new();
            var handlerWarp = new HandlerWarp<T>(GetNextId(), Handler)
            {
                BindingSession = session
            };
            AddHandler<T>(handlerWarp);
            
            var id = handlerWarp.Id;

            cancellationToken.Register(() =>
            {
                _logger.LogTrace("Listen once canceled by token, handlerId:{HandlerId}", id);
                tcs.SetCanceled();
            });
            
            // todo cancel by session close
            
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogTrace("Listen once canceled before listen, handlerId:{HandlerId}", id);
                    return default!;
                }

                var result = await tcs.Task;
                return result;
            }
            catch (TaskCanceledException e)
            {
                _logger.LogWarning("Listen once canceled, handlerId:{HandlerId}", id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Listen once failed, handlerId:{HandlerId}", id);
            }
            finally
            {
                RemoveHandler(id);
                _logger.LogTrace("Listen once removed handler, handlerId:{HandlerId}", id);
            }

            return default!;

            void Handler(IDispatcher dispatcher, ISession sender, T message)
            {
                _logger.LogTrace("Listen once received message, session:{SessionId}, message:{Message}", sender.Id,
                    message);
                if (cancellationToken.IsCancellationRequested)
                {
                    if(tcs.Task.Status!=TaskStatus.Canceled)
                        tcs.SetCanceled();
                }
                else
                {
                    tcs.SetResult(message);
                }
            }
        }

        public async Task<TResp?> SendAndListenOnce<TReq, TResp>(ISession session, TReq message,
            CancellationToken token = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var task = HandleOnce<TResp>(session, cts.Token);
            var sentSucceed = await SendAsync(session, message);
            if (!sentSucceed)
            {
                _logger.LogError("SendAndListenOnce canceled, send message failed: {Message}", message);
                cts.Cancel();
                return default;
            }

            return await task;
        }

        public async ValueTask<bool> SendAsync<T>(ISession session, T message)
        {
            using var stream = RecycleMemoryStreamManagerHolder.Shared.GetStream();
            _packetCodec.Encode(message, stream);


            return await session.SendAsync(stream);
        }


        private HandlerId GetNextId()
        {
            return new HandlerId(Interlocked.Increment(ref _idCounter));
        }

        public interface IHandleWarp
        {
            void Call(IDispatcher dispatcher, ISession sender, object message);
            Type Type { get; }
            Delegate HandlerDelegate { get; }

            ISession? BindingSession { get; }
        }

        public class HandlerWarp<T> : IHandleWarp
        {
            public HandlerWarp(HandlerId id, DispatchHandler<T> handler)
            {
                Handler = handler;
                Id = id;
            }

            public HandlerId Id { get; }

            public Type Type => typeof(T);
            public Delegate HandlerDelegate => Handler;

            public ISession? BindingSession { get; set; }
            public DispatchHandler<T> Handler { get; }

            public void Call(IDispatcher dispatcher, ISession sender, object message)
            {
                if (message is T t)
                {
                    Handler.Invoke(dispatcher, sender, t);
                }
            }
        }
    }
}