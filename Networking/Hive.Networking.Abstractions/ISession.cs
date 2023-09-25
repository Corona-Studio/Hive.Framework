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
    /// <typeparam name="TSelf">分包发送者，通常为自己</typeparam>
    public interface ISession<TSelf> : ISender, IReceiver, IShouldDestroySession where TSelf : ISession<TSelf>
    {
        IPEndPoint? LocalEndPoint { get; }
        IPEndPoint? RemoteEndPoint { get; }
        IDataDispatcher<TSelf> DataDispatcher { get; }
        
        ValueTask DoConnect();
        ValueTask DoDisconnect();

        AsyncEventHandler<ReceivedDataEventArgs>? OnDataReceived { get; set; }
    }
}