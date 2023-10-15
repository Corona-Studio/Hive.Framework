using Hive.Both.General.Channels;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Hive.Server.Common.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Server.Common.Application;

public abstract class ServerApplicationBase : IServerApplication
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<IServerMessageChannel> _channels = new();

    protected ServerApplicationBase(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public Task OnStartAsync(IAcceptor acceptor, IDispatcher dispatcher,CancellationToken stoppingToken)
    {
        var channelHandlerBinder = ChannelHandlerBinderProvider.GetHandlerBinder(this.GetType());
        return channelHandlerBinder.BindAndStart(this, dispatcher, _serviceProvider.GetRequiredService<ILoggerFactory>(), stoppingToken);
    }

    public virtual void OnSessionCreated(ISession session)
    {
        
    }

    public virtual void OnSessionClosed(ISession session)
    {
        
    }
    
}