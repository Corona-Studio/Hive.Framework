using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace Hive.Framework.Shared
{
    public class AsyncEventHandler<TEventArgs> where TEventArgs : EventArgs
    {
        private readonly List<Func<object, TEventArgs, Task>> _invocationList;
        private readonly object _locker;

        private AsyncEventHandler()
        {
            _invocationList = new List<Func<object, TEventArgs, Task>>();
            _locker = new object();
        }

        public static AsyncEventHandler<TEventArgs> operator +(
            AsyncEventHandler<TEventArgs>? e, Func<object, TEventArgs, Task> callback)
        {
            if (callback == null) throw new NullReferenceException("callback is null");

            //Note: Thread safety issue- if two threads register to the same event (on the first time, i.e when it is null)
            //they could get a different instance, so whoever was first will be overridden.
            //A solution for that would be to switch to a public constructor and use it, but then we'll 'lose' the similar syntax to c# events             
            e ??= new AsyncEventHandler<TEventArgs>();

            lock (e._locker)
            {
                e._invocationList.Add(callback);
            }
            return e;
        }

        public static AsyncEventHandler<TEventArgs>? operator -(
            AsyncEventHandler<TEventArgs>? e, Func<object, TEventArgs, Task> callback)
        {
            if (callback == null) throw new NullReferenceException("callback is null");
            if (e == null) return null;

            lock (e._locker)
            {
                e._invocationList.Remove(callback);
            }
            return e;
        }

        public async Task InvokeAsync(object sender, TEventArgs eventArgs)
        {
            List<Func<object, TEventArgs, Task>> tmpInvocationList;
            lock (_locker)
            {
                tmpInvocationList = new List<Func<object, TEventArgs, Task>>(_invocationList);
            }

            foreach (var callback in tmpInvocationList)
            {
                //Assuming we want a serial invocation, for a parallel invocation we can use Task.WhenAll instead
                await callback(sender, eventArgs);
            }
        }
    }
}