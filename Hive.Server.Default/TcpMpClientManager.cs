using Hive.Framework.Networking.Shared;
using Hive.Framework.Networking.Tcp;
using Hive.Server.Default.Messages;

namespace Hive.Server.Default;

public class TcpMpClientManager : AbstractClientManager<int,TcpSession<ushort>>
{
    public override int SessionIdSize => sizeof(int);
    
    public int ConnectedClient { get; private set; }
    
    private int _currentSessionId = 0; // 必须使用原子操作
    
    public override ReadOnlyMemory<byte> GetEncodedC2SSessionPrefix(TcpSession<ushort> session)
    {
        var sessionId = GetSessionId(session);
        // todo 有Memory池之类的东西吗？还是说我自己用ArrayPool？我自己使用ArrayPool的话，我什么时候释放呢？
        // todo Memory的分配应该统一由框架来做，这样可以统一管理，而不是在每个ClientManager里面自己创建。
        var array = new byte[SessionIdSize];
        BitConverter.TryWriteBytes(array, sessionId);
        var memory = new Memory<byte>(array);
        return memory;
    }

    public override int ResolveSessionPrefix(ReadOnlyMemory<byte> payload)
    {
        // todo 这个startIndex的不应该要ClientManager来算，应该要Codec来算，因为不同的Codec的startIndex不一样。
        const int startIndex = 2 + 4 + sizeof(ushort);
        payload = payload.Slice(startIndex, SessionIdSize);
        var span = payload.Span;
        var sessionId = BitConverter.ToInt32(span);
        return sessionId;
    }

    protected override void RegisterHeartBeatMessage(TcpSession<ushort> session)
    {
        session.OnReceive<HeartBeatMessage>((_, tcpSession) =>
        {
            var sessionId = GetSessionId(tcpSession);

            UpdateHeartBeatReceiveTime(sessionId);
        });
    }

    protected override void RegisterSigninMessage(TcpSession<ushort> session)
    {
        session.OnReceive<SigninMessage>((message, tcpSession) =>
        {
            InvokeOnClientConnected(tcpSession);
        });
    }

    protected override void RegisterClientSignOutMessage(TcpSession<ushort> session)
    {
        session.OnReceive<SignOutMessage>((message, tcpSession) =>
        {
            var sessionId = GetSessionId(tcpSession);
            InvokeOnClientDisconnected(sessionId, tcpSession, true);
        });
    }

    protected override void RegisterReconnectMessage(TcpSession<ushort> session)
    {
        session.OnReceive<ReconnectMessage>((_, tcpSession) =>
        {
            var sessionId = GetSessionId(tcpSession);
            InvokeOnClientReconnected(tcpSession, sessionId, true);
        });
    }

    protected override int CreateNewSessionId()
    {
        Interlocked.Increment(ref _currentSessionId);
        return _currentSessionId;
    }
}