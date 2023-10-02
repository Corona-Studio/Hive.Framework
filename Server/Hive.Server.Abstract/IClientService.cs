using System.Net;
using Microsoft.Extensions.Hosting;

namespace Hive.Server.Abstract;

public interface IClientService : IHostedService
{
    ClientHandle? GetClientHandle(ClientId clientId);
    
    void KickClient(ClientId clientId);
}