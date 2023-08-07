﻿using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Hive.Framework.Networking.Shared
{
    public class DefaultDataDispatcher<TSender> : IDataDispatcher<TSender> where TSender : ISession<TSender>
    {
        private readonly ConcurrentDictionary<Type, GuaranteedDeliveryBroadcastBlock<(IPacketDecodeResult<object>, TSender)>> _registeredDataFlow = new ();
        private readonly ConcurrentDictionary<object, List<IDisposable>> _registeredLinks = new ();
        private readonly ConcurrentDictionary<Type, List<IDisposable>> _typedRegisteredLinks = new();

        private void AddCallbackLog(object callback, params IDisposable[] links)
        {
            _registeredLinks.AddOrUpdate(
                callback,
                new List<IDisposable>(links),
                (_, list) =>
                {
                    list.AddRange(links);

                    return list;
                });
        }

        private void AddTypedCallbackLog(Type callbackType, params IDisposable[] links)
        {
            _typedRegisteredLinks.AddOrUpdate(
                callbackType,
                new List<IDisposable>(links),
                (_, list) =>
                {
                    list.AddRange(links);

                    return list;
                });
        }

        #region Register Functions

        public void Register<T>(Action<IPacketDecodeResult<T>, TSender> callback)
        {
            _registeredDataFlow.AddOrUpdate(
                typeof(T),
                new GuaranteedDeliveryBroadcastBlock<(IPacketDecodeResult<object>, TSender)>(tuple => tuple, new DataflowBlockOptions
                {
                    EnsureOrdered = true
                }),
                (_, block) => block);

            var broadcastBlock = _registeredDataFlow[typeof(T)];

            var transformBlock = new TransformBlock<(IPacketDecodeResult<object>, TSender), (IPacketDecodeResult<T>, TSender)>(raw =>
            {
                var (rawData, sender) = raw;
                return (new PacketDecodeResult<T>(rawData.Prefixes, rawData.Flags, (T)rawData.Payload), sender);
            });

            var actionBlock = new ActionBlock<(IPacketDecodeResult<T>, TSender)>(data =>
            {
                callback(data.Item1, data.Item2);
            });

            var options = new DataflowLinkOptions
            {
                Append = true,
                PropagateCompletion = true
            };

            var link1 = transformBlock.LinkTo(actionBlock, options);
            var link2 = broadcastBlock.LinkTo(transformBlock, options);

            AddCallbackLog(callback, link1, link2);
            AddTypedCallbackLog(typeof(T), link1, link2);
        }

        public void OneTimeRegister<T>(Action<IPacketDecodeResult<T>, TSender> callback)
        {
            _registeredDataFlow.AddOrUpdate(
                typeof(T),
                new GuaranteedDeliveryBroadcastBlock<(IPacketDecodeResult<object>, TSender)>(tuple => tuple, new DataflowBlockOptions
                {
                    EnsureOrdered = true
                }),
                (_, block) => block);

            var broadcastBlock = _registeredDataFlow[typeof(T)];

            var writeOnceBlock = new WriteOnceBlock<(IPacketDecodeResult<object>, TSender)>(tuple => tuple);

            var transformBlock = new TransformBlock<(IPacketDecodeResult<object>, TSender), (IPacketDecodeResult<T>, TSender)>(raw =>
            {
                var (rawData, sender) = raw;
                return (new PacketDecodeResult<T>(rawData.Prefixes, rawData.Flags, (T)rawData.Payload), sender);
            });

            var actionBlock = new ActionBlock<(IPacketDecodeResult<T>, TSender)>(data =>
            {
                callback(data.Item1, data.Item2);
                Unregister(callback);
            });

            var options = new DataflowLinkOptions
            {
                Append = true,
                PropagateCompletion = true
            };

            var link1 = writeOnceBlock.LinkTo(transformBlock, options);
            var link2 = transformBlock.LinkTo(actionBlock, options);
            var link3 = broadcastBlock.LinkTo(writeOnceBlock, options);

            AddCallbackLog(callback, link1, link2, link3);
            AddTypedCallbackLog(typeof(T), link1, link2, link3);
        }

        public void Unregister<T>(Action<IPacketDecodeResult<T>, TSender> callback)
        {
            if (!_registeredLinks.TryRemove(callback, out var links)) return;

            foreach (var disposable in links)
                try
                {
                    disposable.Dispose();
                }
                catch (Exception) { }
        }

        #endregion

        #region Register Functions (Async)

        public void Register<T>(Func<IPacketDecodeResult<T>, TSender, ValueTask> callback)
        {
            _registeredDataFlow.AddOrUpdate(
                typeof(T),
                new GuaranteedDeliveryBroadcastBlock<(IPacketDecodeResult<object>, TSender)>(tuple => tuple, new DataflowBlockOptions
                {
                    EnsureOrdered = true
                }),
                (_, block) => block);

            var broadcastBlock = _registeredDataFlow[typeof(T)];

            var transformBlock = new TransformBlock<(IPacketDecodeResult<object>, TSender), (IPacketDecodeResult<T>, TSender)>(raw =>
            {
                var (rawData, sender) = raw;
                return (new PacketDecodeResult<T>(rawData.Prefixes, rawData.Flags, (T)rawData.Payload), sender);
            });

            var actionBlock = new ActionBlock<(IPacketDecodeResult<T>, TSender)>(async data =>
            {
                await callback(data.Item1, data.Item2);
            });

            var options = new DataflowLinkOptions
            {
                Append = true,
                PropagateCompletion = true
            };

            var link1 = transformBlock.LinkTo(actionBlock, options);
            var link2 = broadcastBlock.LinkTo(transformBlock, options);

            AddCallbackLog(callback, link1, link2);
            AddTypedCallbackLog(typeof(T), link1, link2);
        }

        public void OneTimeRegister<T>(Func<IPacketDecodeResult<T>, TSender, ValueTask> callback)
        {
            _registeredDataFlow.AddOrUpdate(
                typeof(T),
                new GuaranteedDeliveryBroadcastBlock<(IPacketDecodeResult<object>, TSender)>(tuple => tuple, new DataflowBlockOptions
                {
                    EnsureOrdered = true
                }),
                (_, block) => block);

            var broadcastBlock = _registeredDataFlow[typeof(T)];

            var writeOnceBlock = new WriteOnceBlock<(IPacketDecodeResult<object>, TSender)>(tuple => tuple);

            var transformBlock = new TransformBlock<(IPacketDecodeResult<object>, TSender), (IPacketDecodeResult<T>, TSender)>(raw =>
            {
                var (rawData, sender) = raw;
                return (new PacketDecodeResult<T>(rawData.Prefixes, rawData.Flags, (T)rawData.Payload), sender);
            });

            var actionBlock = new ActionBlock<(IPacketDecodeResult<T>, TSender)>(async data =>
            {
                await callback(data.Item1, data.Item2);
                Unregister(callback);
            });

            var options = new DataflowLinkOptions
            {
                Append = true,
                PropagateCompletion = true
            };

            var link1 = writeOnceBlock.LinkTo(transformBlock, options);
            var link2 = transformBlock.LinkTo(actionBlock, options);
            var link3 = broadcastBlock.LinkTo(writeOnceBlock, options);

            AddCallbackLog(callback, link1, link2, link3);
            AddTypedCallbackLog(typeof(T), link1, link2, link3);
        }

        public void Unregister<T>(Func<IPacketDecodeResult<T>, TSender, ValueTask> callback)
        {
            if (!_registeredLinks.TryRemove(callback, out var links)) return;

            foreach (var disposable in links)
                try
                {
                    disposable.Dispose();
                }
                catch (Exception) { }
        }

        #endregion

        public void UnregisterAll<T>()
        {
            if (_registeredDataFlow.TryRemove(typeof(T), out var block))
                block.Complete();
            if(_typedRegisteredLinks.TryRemove(typeof(T), out var links))
                foreach (var disposable in links)
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception) { }
        }

        public void UnregisterAll()
        {
            foreach (var (_, broadcastBlock) in _registeredDataFlow)
                broadcastBlock.Complete();
            foreach (var (_, links) in _registeredLinks)
            foreach (var disposable in links)
                try
                {
                    disposable.Dispose();
                }
                catch (Exception) { }

            _registeredDataFlow.Clear();
            _registeredLinks.Clear();
            _typedRegisteredLinks.Clear();
        }

        public async ValueTask DispatchAsync(TSender sender, IPacketDecodeResult<object> data, Type? dataType = null)
        {
            if (!_registeredDataFlow.TryGetValue(dataType ?? data.GetType(), out var block)) return;

            await block.SendAsync((data, sender));
        }
    }
}