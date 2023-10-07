using Microsoft.Extensions.Hosting;

namespace Hive.Server.Abstractions;

public interface IClientService : IHostedService
{
    ClientHandle? GetClientHandle(ClientId clientId);

    void KickClient(ClientId clientId);
}