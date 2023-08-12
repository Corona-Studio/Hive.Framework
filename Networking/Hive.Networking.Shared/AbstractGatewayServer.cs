using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Hive.Framework.Codec.Abstractions;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Abstractions.EventArgs;
using Hive.Framework.Shared;
using Hive.Framework.Shared.Helpers;

namespace Hive.Framework.Networking.Shared;

/// <summary>
/// 网关服务器抽象
/// </summary>
/// <typeparam name="TSession"></typeparam>
/// <typeparam name="TSessionId"></typeparam>
/// <typeparam name="TId"></typeparam>
public abstract class AbstractGatewayServer<TSession, TSessionId, TId> : IGatewayServer<TSession, TSessionId, TId>
    where TSession : ISession<TSession>
    where TSessionId : unmanaged
    where TId : unmanaged
{
    protected readonly ConcurrentDictionary<TId, ILoadBalancer<TSession>> PacketRouteTable = new ();

    public IPacketCodec<TId> PacketCodec { get; }
    public IAcceptorImpl<TSession, TSessionId> Acceptor { get; }

    /// <summary>
    /// 负载均衡器创建方法
    /// <para>该方法用于为刚注册的服务器创建一个初始化的负载均衡器</para>
    /// <para>方法的第一个参数 <see cref="TSession"/> 为首个注册的服务器，您需要在该方法中初始化负载均衡器并将这个服务器添加到负载均衡器当中。</para>
    /// </summary>
    public Func<TSession, ILoadBalancer<TSession>> LoadBalancerGetter { get; }

    /// <summary>
    /// 负载均衡器设置事件
    /// <para>该方法会在服务器添加到负载均衡器后进行调用，用于后续的额外设置（例如设置该服务器的权重）</para>
    /// <para>该方法的第一个参数 <see cref="ILoadBalancer{TSession}"/> 会传入当前的负载均衡器，第二个参数 <see cref="TSession"/> 会传入当前添加的会话</para>
    /// </summary>
    public event EventHandler<LoadBalancerInitializedEventArgs<TSession>> OnLoadBalancerInitialized;

    protected AbstractGatewayServer(
        IPacketCodec<TId> packetCodec,
        IAcceptorImpl<TSession, TSessionId> acceptor,
        Func<TSession, ILoadBalancer<TSession>> loadBalancerGetter)
    {
        PacketCodec = packetCodec;
        Acceptor = acceptor;
        LoadBalancerGetter = loadBalancerGetter;

        acceptor.ClientManager.OnClientConnectionStateChanged += ClientManagerOnOnClientConnectionStateChanged;
    }

    public virtual void StartServer()
    {
        Acceptor.Start();
    }

    public virtual void StopServer()
    {
        Acceptor.Stop();
    }

    protected virtual void AddPacketRoute(TId packetId, TSession session)
    {
        PacketRouteTable.AddOrUpdate(packetId, LoadBalancerGetter(session), (_, loadBalancer) =>
        {
            loadBalancer.AddSession(session);
            return loadBalancer;
        });

        OnLoadBalancerInitialized?.Invoke(this, new LoadBalancerInitializedEventArgs<TSession>(PacketRouteTable[packetId], session));
    }

    /// <summary>
    /// 客户端连接建立提示告知方法
    /// <para>一般情况下，此方法需要在 <see cref="RegisterClientStartTransmitMessage"/> 的事件处理程序的完成阶段调用</para>
    /// <para>使用此方法告知客户端可以开始进行正文的传输</para>
    /// <para>客户端只应该在接受到该方法发送的消息后才开始进行数据传输，否则可能会导致前半部分数据丢失</para>
    /// </summary>
    /// <param name="session"></param>
    protected abstract ValueTask NotifyClientCanStartTransmitMessage(TSession session);

    /// <summary>
    /// 服务器注册方法
    /// <para>一般情况下，此方法需要注册相应的数据包来完成数据包路由表注册，通过更新 <see cref="PacketRouteTable"/> 来完成注册</para>
    /// <para>该路由表会帮助网关将对应的数据包发送至相应的服务器</para>
    /// <para>注册信息中至少需要包括：服务器 Tag （Tag 相同的服务器注册后将使用负载均衡策略），需要转发的数据包列表</para>
    /// </summary>
    /// <param name="session"></param>
    protected abstract void RegisterServerRegistrationMessage(TSession session);

    /// <summary>
    /// 客户端数据包传送起始注册方法
    /// <para>一般情况下，此方法需要在接受到相应的起始数据包后开始转发该客户端会话发送的所有请求，通过调用 <see cref="DoForwardDataToServerAsync"/> 来将数据包转发给对应服务器</para>
    /// </summary>
    /// <param name="session"></param>
    protected abstract void RegisterClientStartTransmitMessage(TSession session);

    /// <summary>
    /// 客户端连接触发事件
    /// <para>默认情况下，该方法会自动注册 <see cref="RegisterServerRegistrationMessage"/> 以及 <see cref="RegisterClientStartTransmitMessage"/> 事件</para>
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected virtual void InvokeOnClientConnected(ClientConnectionChangedEventArgs<TSession> e)
    {
        RegisterServerRegistrationMessage(e.Session);
        RegisterClientStartTransmitMessage(e.Session);
    }

    /// <summary>
    /// 客户端连接断开触发事件
    /// <para>默认情况下，如果重连的会话是服务器会话，则该方法会遍历<see cref="PacketRouteTable"/> 来将其标记为不再可用。</para>
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected virtual void InvokeOnClientDisconnected(ClientConnectionChangedEventArgs<TSession> e)
    {
        foreach (var (_, loadBalancer) in PacketRouteTable)
            loadBalancer.UpdateSessionAvailability(e.Session, false);
    }

    /// <summary>
    /// 客户端重连触发事件
    /// <para>默认情况下，如果重连的会话是服务器会话，则该方法会遍历<see cref="PacketRouteTable"/> 来重新恢复他的可用性。</para>
    /// </summary>
    /// <param name="e"></param>
    protected virtual void InvokeOnClientReconnected(ClientConnectionChangedEventArgs<TSession> e)
    {
        foreach (var (_, loadBalancer) in PacketRouteTable)
            loadBalancer.UpdateSessionAvailability(e.Session, true);
    }

    private void ClientManagerOnOnClientConnectionStateChanged(object sender, ClientConnectionChangedEventArgs<TSession> e)
    {
        switch (e.Status)
        {
            case ClientConnectionStatus.Connected:
                InvokeOnClientConnected(e);
                break;
            case ClientConnectionStatus.Disconnected:
                InvokeOnClientDisconnected(e);
                break;
            case ClientConnectionStatus.Reconnected:
                InvokeOnClientReconnected(e);
                break;
            default:
                throw new InvalidOperationException($"Unknown client connection status: {e.Status}");
        }
    }

    /// <summary>
    /// 客户端数据转发方法
    /// <para>一般情况下，此方法会拆解客户端数据包，并向其追加客户端会话 ID 等信息，并将数据包转发给相应服务器</para>
    /// <para>注意：在调用该转发方法时，应始终保持 [封包 ID] 的下一位为 [客户端会话 ID]，并且服务端也应该从 [客户端会话 ID] 部分开始解析自定义包头</para>
    /// </summary>
    /// <param name="session"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    protected virtual async ValueTask DoForwardDataToServerAsync(TSession session, ReadOnlyMemory<byte> data)
    {
        var packetIdMemory = PacketCodec.GetPacketIdMemory(data);
        var packetId = PacketCodec.GetPacketId(packetIdMemory);
        
        if (!GetServerSession(packetId, true, out var serverSession)) return;

        // [LENGTH (2) | PACKET_FLAGS (4) | PACKET_ID | SESSION_ID | PAYLOAD]
        var clientSessionIdMemory = Acceptor.ClientManager.GetEncodedC2SSessionPrefix(session);

        var currentPacketFlagsMemory = PacketCodec.GetPacketFlagsMemory(data);
        var currentPacketFlags = (PacketFlags)BitConverter.ToUInt32(currentPacketFlagsMemory.Span);
        var newPacketFlags = currentPacketFlags | PacketFlags.HasCustomPacketPrefix;
        var packetFlagsMemory = BitConverter.GetBytes((uint)newPacketFlags).AsMemory();

        var payload = data[(2 + 4 + packetIdMemory.Length)..];
        var resultLength = packetFlagsMemory.Length + packetIdMemory.Length + clientSessionIdMemory.Length + payload.Length;
        var lengthMemory = BitConverter.GetBytes((ushort)resultLength).AsMemory();

        var repackedData =
            MemoryHelper.CombineMemory(
                lengthMemory,
                packetFlagsMemory,
                packetIdMemory,
                clientSessionIdMemory,
                payload);

        // 按照顺序发送数据
        await serverSession!.SendAsync(repackedData);
    }

    /// <summary>
    /// 服务端数据转发方法
    /// <para>一般情况下，此方法会拆解服务端数据包，并解析其中包含的客户端会话 ID 等信息，并将数据包转发给相应客户端</para>
    /// <para>注意：在调用该转发方法时，应始终保持 [封包 ID] 的下一位为 [客户端会话 ID]，并且服务端也应该从 [客户端会话 ID] 部分开始解析自定义包头</para>
    /// </summary>
    /// <param name="session"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    protected virtual async ValueTask DoForwardDataToClientAsync(ReadOnlyMemory<byte> data)
    {
        var packetIdMemory = PacketCodec.GetPacketIdMemory(data);
        var packetFlags = PacketCodec.GetPacketFlags(data);

        if (!packetFlags.HasFlag(PacketFlags.S2CPacket)) return;

        var sessionId = Acceptor.ClientManager.ResolveSessionPrefix(data);

        if (!Acceptor.ClientManager.TryGetSession(sessionId, out var session))
            return;

        var newFlag = packetFlags | PacketFlags.Finalized;
        var packetFlagsMemory = BitConverter.GetBytes((uint) newFlag);
        var payload = data[(2 + 4 + packetIdMemory.Length + Acceptor.ClientManager.SessionIdSize)..];
        var resultLength = packetFlagsMemory.Length + packetIdMemory.Length + payload.Length;
        var lengthMemory = BitConverter.GetBytes((ushort)resultLength).AsMemory();

        // [LENGTH (2) | PACKET_FLAGS (4) | PACKET_ID | SESSION_ID | PAYLOAD]
        var repackedData =
            MemoryHelper.CombineMemory(
                lengthMemory,
                packetFlagsMemory,
                packetIdMemory,
                payload);

        await session!.SendAsync(repackedData);
    }

    protected virtual bool GetServerSession(TId packetId, bool useLoadBalancer, out TSession? serverSession)
    {
        if (!PacketRouteTable.TryGetValue(packetId, out var loadBalancer))
        {
            serverSession = default;
            return false;
        }

        serverSession = useLoadBalancer? loadBalancer.GetOne() : loadBalancer.GetRawList()[0];
        return true;
    }

    public virtual bool ServerInitialized => !PacketRouteTable.IsEmpty;

    public virtual void Dispose()
    {
        Acceptor.Dispose();
    }
}