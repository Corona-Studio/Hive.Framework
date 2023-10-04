using Hive.Framework.Codec.Abstractions;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Network.Tests;

public class ServiceProviderHelper
{
    public static IServiceProvider GetServiceProvider<TSession,TAcceptor,TConnector,TCodec>() 
        where TAcceptor : class, IAcceptor<TSession>
        where TConnector : class, IConnector<TSession>
        where TSession : class, ISession 
        where TCodec : class, IPacketCodec
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        services.AddSingleton<IPacketCodec,TCodec>();
        services.AddTransient<ISession,TSession>();
        services.AddSingleton<IAcceptor<TSession>,TAcceptor>();
        services.AddSingleton<IConnector<TSession>, TConnector>();
            
        return services.BuildServiceProvider();
    }
}