using Hive.Both.General;
using Hive.Codec.Shared;
using Hive.Framework.Codec.Abstractions;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Hive.Network.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Application.Test;

public static class ServiceProviderHelper
{
    public static void BuildSession<TSession,TAcceptor,TConnector,TCodec>(this ServiceCollection services)
        where TAcceptor : class, IAcceptor<TSession>
        where TConnector : class, IConnector<TSession>
        where TSession : class, ISession 
        where TCodec : class, IPacketCodec
    {
        services.AddSingleton<ICustomCodecProvider, DefaultCustomCodecProvider>();
        services.AddSingleton<IPacketIdMapper, DefaultPacketIdMapper>();
        services.AddSingleton<IPacketCodec,TCodec>();
        services.AddTransient<ISession,TSession>();
        services.AddSingleton<IAcceptor<TSession>,TAcceptor>();
        services.AddSingleton<IConnector<TSession>, TConnector>();
    }
    
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
        
        services.BuildSession<TSession,TAcceptor,TConnector,TCodec>();

        services.AddSingleton<IDispatcher, DefaultDispatcher>();
            
        return services.BuildServiceProvider();
    }
}