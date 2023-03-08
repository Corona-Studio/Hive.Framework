using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Abstractions.EventArgs;
using Hive.Framework.Shared;

namespace Hive.Framework.Networking.Shared;

/// <summary>
/// 网关服务器抽象
/// </summary>
/// <typeparam name="TSession"></typeparam>
/// <typeparam name="TSessionId"></typeparam>
/// <typeparam name="TId"></typeparam>
public abstract class AbstractGatewayServer<TSession, TSessionId, TId> : IGatewayServer<TSession, TSessionId, TId> where TSession : ISession<TSession> where TId : unmanaged
{
    protected readonly ConcurrentDictionary<TId, TSession> PacketRouteTable = new ();

    public IPacketCodec<TId> PacketCodec { get; }
    public IAcceptorImpl<TSession, TSessionId> Acceptor { get; }
    public TId[]? ExcludeRedirectPacketIds { get; }

    protected AbstractGatewayServer(IPacketCodec<TId> packetCodec, IAcceptorImpl<TSession, TSessionId> acceptor, TId[]? excludeRedirectPacketIds)
    {
        PacketCodec = packetCodec;
        Acceptor = acceptor;
        ExcludeRedirectPacketIds = excludeRedirectPacketIds;
    }

    public virtual void StartServer()
    {
        Acceptor.Start();
    }

    public virtual void StopServer()
    {
        Acceptor.Stop();
    }

    /// <summary>
    /// 服务器注册方法
    /// <para>一般情况下，此方法需要注册相应的数据包来完成数据包路由表注册，通过更新 <see cref="PacketRouteTable"/> 来完成注册</para>
    /// <para>该路由表会帮助网关将对应的数据包发送至相应的服务器</para>
    /// </summary>
    /// <param name="session"></param>
    protected abstract void RegisterServerRegistrationMessage(TSession session);
    /// <summary>
    /// 客户端数据包传送起始注册方法
    /// <para>一般情况下，此方法需要在接受到相应的起始数据包后开始转发该客户端会话发送的所有请求，通过调用 <see cref="DoForwardDataToServer"/> 来将数据包转发给对应服务器</para>
    /// </summary>
    /// <param name="session"></param>
    protected abstract void RegisterClientStartTransmitMessage(TSession session);

    /// <summary>
    /// 客户端连接触发事件
    /// <para>默认情况下，该方法会自动注册 <see cref="RegisterServerRegistrationMessage"/> 以及 <see cref="RegisterClientStartTransmitMessage"/> 事件</para>
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected virtual void InvokeOnClientConnected(object sender, ClientConnectionChangedEventArgs<TSession> e)
    {
        RegisterServerRegistrationMessage(e.Session);
        RegisterClientStartTransmitMessage(e.Session);
    }

    /// <summary>
    /// 客户端连接断开触发事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected abstract void InvokeOnClientDisconnected(object sender, ClientConnectionChangedEventArgs<TSession> e);

    /// <summary>
    /// 客户端数据转发方法
    /// <para>一般情况下，此方法会拆解客户端数据包，并向其追加客户端会话 ID 等信息，并将数据包转发给相应服务器</para>
    /// </summary>
    /// <param name="session"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public virtual ValueTask DoForwardDataToServer(TSession session, ReadOnlyMemory<byte> data)
    {
        var packetIdMemory = PacketCodec.GetPacketIdMemory(data);
        var packetId = PacketCodec.GetPacketId(packetIdMemory);

        if (PacketRouteTable.TryGetValue(packetId, out var serverSession))
        {
            // [LENGTH (2) | SESSION_ID | PAYLOAD]
            var clientSessionIdMemory = Acceptor.ClientManager.GetEncodedSessionId(session);
            var payload = data[2..];
            var length = BitConverter.GetBytes((ushort)(clientSessionIdMemory.Length + payload.Length + 2)).AsMemory();
            var totalLength = length.Length + clientSessionIdMemory.Length + payload.Length;

            // 按照顺序发送数据
            serverSession.Send(MemoryHelper.CombineMemory(totalLength, length, clientSessionIdMemory, payload));
        }

        return default;
    }

    public bool ServerInitialized => !PacketRouteTable.IsEmpty;
}