using Hive.Codec.MemoryPack;
using Hive.Framework.Codec.Abstractions;
using Hive.Network.Tcp;
using Hive.Server.Abstract;
using Hive.Server.Default;
using Microsoft.Extensions.DependencyInjection;

namespace Hive.Server.Shared;

public static class AppBuilder
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPacketCodec,MemoryPackPacketCodec>();
        services.AddSingleton<IClientService,DefaultClientService<TcpSession>>();
    }
}