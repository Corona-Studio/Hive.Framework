using Hive.Framework.Codec.Abstractions;
using System;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions
{
    /// <summary>
    /// 表示一个数据分发器
    /// <para>所有数据分发器都应该加入对 <see cref="INoPayloadPacketPlaceHolder"/> 的特殊实现</para>
    /// <para>在数据分发器遇到没有负载的特殊封包时，都应该将这些数据转发到 <see cref="INoPayloadPacketPlaceHolder"/></para>
    /// </summary>
    /// <typeparam name="TSession">发送者，通常为对应协议的 Session</typeparam>
    public interface IDataDispatcher<TSession> where TSession : ISession<TSession>
    {
        void Register<T>(Action<PacketDecodeResult<T>, TSession> callback); 
        void OneTimeRegister<T>(Action<PacketDecodeResult<T>, TSession> callback);
        void Unregister<T>(Action<PacketDecodeResult<T>, TSession> callback);

        void Register<T>(Func<PacketDecodeResult<T>, TSession, ValueTask> callback);
        void OneTimeRegister<T>(Func<PacketDecodeResult<T>, TSession, ValueTask> callback);
        void Unregister<T>(Func<PacketDecodeResult<T>, TSession, ValueTask> callback);

        void UnregisterAll<T>();
        void UnregisterAll();
        ValueTask DispatchAsync(TSession sender, PacketDecodeResult<object?> data, Type? dataType = null);
    }
}