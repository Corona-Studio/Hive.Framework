using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;

namespace Hive.Server.Common.Abstract;

public interface IServerApplication
{
    Task OnStartAsync(IAcceptor acceptor, IDispatcher dispatcher,CancellationToken stoppingToken);
    
    void OnSessionCreated(ISession session);
    
    void OnSessionClosed(ISession session);
}