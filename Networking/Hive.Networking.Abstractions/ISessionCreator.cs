using System.Net;

namespace Hive.Framework.Networking.Abstractions;

public interface ISessionCreator<out TSession, in TSocket> where TSession : ISession<TSession>
{
    public TSession CreateSession(TSocket socket,IPEndPoint endPoint);
}