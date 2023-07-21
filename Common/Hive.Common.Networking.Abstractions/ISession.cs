using System;
using System.Net;
using System.Threading.Tasks;
using Hive.Framework.Networking.Abstractions.EventArgs;

namespace Hive.Framework.Networking.Abstractions
{
    /// <summary>
    /// 代表一个连接会话
    /// </summary>
    /// <typeparam name="TSender">分包发送者，通常为自己</typeparam>
    public interface ISession<TSender> : IShouldDestroySession where TSender : ISession<TSender>
    {
        IPEndPoint? LocalEndPoint { get; }
        IPEndPoint RemoteEndPoint { get; }
        IDataDispatcher<TSender> DataDispatcher { get; }
        
        ValueTask DoConnect();
        ValueTask DoDisconnect();
        ValueTask Send(ReadOnlyMemory<byte> data);
        ValueTask SendOnce(ReadOnlyMemory<byte> data);
        ValueTask<int> ReceiveOnce(Memory<byte> buffer);

        event EventHandler<ReceivedDataEventArgs>? OnDataReceived;
    }
}