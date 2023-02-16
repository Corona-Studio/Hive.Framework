using System.Collections.Generic;
using System;

namespace Hive.Framework.Networking.Abstractions
{
    public interface IDataDispatcher<TSender>
    {
        Dictionary<Type, CallbackWarp> CallbackDictionary { get; }

        protected void CheckCallbackDictionary(Type type)
        {
            if (!CallbackDictionary.ContainsKey(type)) CallbackDictionary.Add(type, new CallbackWarp());
        }

        public void Register<T>(Action<T, TSender> callback)
        {
            CheckCallbackDictionary(typeof(T));
            CallbackDictionary[typeof(T)].AddCallback(callback);
        }


        public void Unregister<T>(Action<T, TSender> callback)
        {
            CheckCallbackDictionary(typeof(T));
            CallbackDictionary[typeof(T)].RemoveCallback(callback);
        }


        public bool Dispatch(TSender sender, object data, Type? dataType = null)
        {
            var type = dataType ?? data.GetType();

            if (!CallbackDictionary.ContainsKey(type)) return false;

            var callbackWarp = CallbackDictionary[type];

            return callbackWarp.InvokeAll(data, sender);
        }

        public readonly struct CallbackWarp
        {
            private readonly List<(object, Action<object, TSender>)> _uniformCallback;
            //private readonly List<DynamicCallback<TSender>> UniformCallback;

            public CallbackWarp()
            {
                //UniformCallback = new List<DynamicCallback<TSender>>();
                _uniformCallback = new List<(object, Action<object, TSender>)>();
            }

            public void AddCallback<T>(Action<T, TSender> callback)
            {
                //var caller = DynamicCallback<TSender>.GetCaller(callback);
                //UniformCallback.Add(caller);
                _uniformCallback.Add((callback, (obj, sender) => callback((T)obj, sender)));
            }

            public void RemoveCallback<T>(Action<T, TSender> callback)
            {
                _uniformCallback.RemoveAll(t
                    => callback.Equals(t.Item1));
            }

            public bool InvokeAll(object msg, TSender sender)
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