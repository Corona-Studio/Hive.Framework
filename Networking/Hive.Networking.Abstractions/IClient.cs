using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions;

public interface IClient<TSession> where TSession : ISession<TSession>
{
    
    ValueTask DoConnect();
    ValueTask DoDisconnect();
}