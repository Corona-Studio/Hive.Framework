using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions
{
    /// <summary>
    /// 代表一个连接，可以是服务端之间的连接，也可以是客户端之间的连接
    /// </summary>
    public partial interface ISession
    {
        public int Id { get; }
        
        IPEndPoint? LocalEndPoint { get; }
        IPEndPoint? RemoteEndPoint { get; }
        
        /// <summary>
        /// 收到数据后的回调，不需要IO，无需异步
        /// </summary>
        event Action<ISession, ReadOnlyMemory<byte>> OnMessageReceived;

        public Task StartAsync(CancellationToken token);
        

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        ValueTask<bool> SendAsync(IMessageStream stream, CancellationToken token=default);
    }
}