using System;
using System.Net;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions
{
    /// <summary>
    /// 代表一个连接
    /// </summary>
    public interface ISession<TSender> : IDisposable
    {
        IPEndPoint? LocalEndPoint { get; }
        IPEndPoint? RemoteEndPoint { get; }
        IDataDispatcher<TSender> DataDispatcher { get; }

        ValueTask DoConnect();
        ValueTask DoDisconnect();
        ValueTask SendOnce(ReadOnlyMemory<byte> data);
        ValueTask<int> ReceiveOnce(Memory<byte> buffer);
    }
}