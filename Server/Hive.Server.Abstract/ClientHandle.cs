using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;

namespace Hive.Server.Abstract;

public class ClientHandle
{
    public ClientHandle(ClientId id, ISession session)
    {
        Id = id;
        Session = session;
    }

    public ClientId Id { get; }
    public ISession Session { get; }
    
    public SessionId SessionId => Session.Id;
    public long LastHeartBeatTimeUtc { get; set; }
}