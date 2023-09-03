using System;
using System.Net;
using System.Threading.Tasks;
using Hive.Framework.Networking.Abstractions.EventArgs;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Abstractions
{
    /// <summary>
    /// 代表一个连接会话
    /// </summary>
    /// <typeparam name="TSender">分包发送者，通常为自己</typeparam>
    public interface ISession<TSender> : ISender, IReceiver, IShouldDestroySession where TSender : ISession<TSender>
    {
        IPEndPoint? LocalEndPoint { get; }
        IPEndPoint? RemoteEndPoint { get; }
        IDataDispatcher<TSender> DataDispatcher { get; }
        
        ValueTask DoConnect();
        ValueTask DoDisconnect();

        AsyncEventHandler<ReceivedDataEventArgs>? OnDataReceived { get; set; }
    }
}