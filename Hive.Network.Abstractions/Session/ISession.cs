using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Network.Abstractions.Session;

/// <summary>
///     代表一个连接，可以是服务端之间的连接，也可以是客户端之间的连接
///     <para>包含了连接的基本信息，以及收发数据的方法</para>
/// </summary>
public interface ISession
{
    public SessionId Id { get; }

    IPEndPoint? LocalEndPoint { get; }
    IPEndPoint? RemoteEndPoint { get; }

    long LastHeartBeatTime { get; }

    /// <summary>
    ///     收到数据后的回调，不需要IO，无需异步。
    ///     拿到后立刻处理，否则数据会被回收。
    ///     <para>如果要缓存，必须复制一份数据</para>
    /// </summary>
    event SessionReceivedHandler OnMessageReceived;


    public Task StartAsync(CancellationToken token);
    
    
    ValueTask SendAsync(MemoryStream ms, CancellationToken token = default);

    /// <summary>
    ///     发送数据
    /// </summary>
    /// <param name="ms"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    ValueTask<bool> TrySendAsync(MemoryStream ms, CancellationToken token = default);

    void Close();
}