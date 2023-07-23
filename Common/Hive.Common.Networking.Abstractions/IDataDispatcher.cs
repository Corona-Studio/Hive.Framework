using System;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions
{
    /// <summary>
    /// 表示一个数据分发器
    /// </summary>
    /// <typeparam name="TSession">发送者，通常为对应协议的 Session</typeparam>
    public interface IDataDispatcher<TSession> where TSession : ISession<TSession>
    {
        void Register<T>(Action<T, TSession> callback); 
        void OneTimeRegister<T>(Action<T, TSession> callback);
        void Unregister<T>(Action<T, TSession> callback);
        void UnregisterAll<T>();
        void UnregisterAll();
        ValueTask DispatchAsync(TSession sender, object data, Type? dataType = null);
    }
}