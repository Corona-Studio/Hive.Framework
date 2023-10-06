using Hive.Codec.Abstractions;
using Hive.Codec.MemoryPack;
using Hive.Network.Tcp;
using Hive.Server.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Hive.Server.Default;

public static class AppBuilder
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPacketCodec,MemoryPackPacketCodec>();
        services.AddSingleton<IClientService,DefaultClientService<TcpSession>>();
    }
}