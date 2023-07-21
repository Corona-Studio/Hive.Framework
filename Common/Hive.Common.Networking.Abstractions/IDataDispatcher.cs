using System.Collections.Generic;
using System;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions
{
    /// <summary>
    /// 表示一个数据分发器
    /// </summary>
    /// <typeparam name="TSession">发送者，通常为对应协议的 Session</typeparam>
    public interface IDataDispatcher<TSession> where TSession : ISession<TSession>
    {
        Channel<(Type, TSession, object)> DataDispatchChannel { get; }
        ConcurrentDictionary<Type, CallbackWarp> CallbackDictionary { get; }

        protected void CheckCallbackDictionary(Type type)
        {
            if (!CallbackDictionary.ContainsKey(type))
                CallbackDictionary.AddOrUpdate(type, new CallbackWarp(), (_, warp) => warp);
        }

        Task StartDispatchLoop();

        public void Register<T>(Action<T, TSession> callback)
        {
            CheckCallbackDictionary(typeof(T));
            CallbackDictionary[typeof(T)].AddCallback(callback);
        }


        public void Unregister<T>(Action<T, TSession> callback)
        {
            CheckCallbackDictionary(typeof(T));
            CallbackDictionary[typeof(T)].RemoveCallback(callback);
        }


        public async Task DispatchAsync(TSession sender, object data, Type? dataType = null)
        {
            var type = dataType ?? data.GetType();

            if (!CallbackDictionary.ContainsKey(type)) return;
            if (await DataDispatchChannel.Writer.WaitToWriteAsync())
                await DataDispatchChannel.Writer.WriteAsync((type, sender, data));
        }

        public readonly struct CallbackWarp
        {
            private readonly List<(object, Action<object, TSession>)> _uniformCallback;
            //private readonly List<DynamicCallback<TSender>> UniformCallback;

            public CallbackWarp()
            {
                //UniformCallback = new List<DynamicCallback<TSender>>();
                _uniformCallback = new List<(object, Action<object, TSession>)>();
            }

            public void AddCallback<T>(Action<T, TSession> callback)
            {
                //var caller = DynamicCallback<TSender>.GetCaller(callback);
                //UniformCallback.Add(caller);
                _uniformCallback.Add((callback, (obj, sender) => callback((T)obj, sender)));
            }

            public void RemoveCallback<T>(Action<T, TSession> callback)
            {
                _uniformCallback.RemoveAll(t
                    => callback.Equals(t.Item1));
            }

            public bool InvokeAll(object msg, TSession sender)
            {
                var hasReceived = false;
                for (var i = 0; i < _uniformCallback.Count; i++)
                {
                    hasReceived = true;
                    _uniformCallback[i].Item2.Invoke(msg, sender);
                    //UniformCallback[i].Invoke(data, sender);
                }

                return hasReceived;
            }
        }
    }
}