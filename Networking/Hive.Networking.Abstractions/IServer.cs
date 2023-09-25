using System.Threading;
using System.Threading.Tasks;

namespace Hive.Framework.Networking.Abstractions;

public interface IServer
{
    
}

public interface IServer<TClientId, TPacketId> where TPacketId : unmanaged
{
    public Task StartAsync(CancellationToken token);
    
    public Task StartAcceptLoop(CancellationToken token);
    
    public Task StopAsync(CancellationToken token);
    
    
    public ValueTask DoAccept();
}