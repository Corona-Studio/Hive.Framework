using System.Diagnostics.CodeAnalysis;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;
using Hive.Server.Common.Abstract;

namespace Hive.Server.Common.Application;

public abstract class ServerApplicationBase : IServerApplication
{
    protected ServerApplicationBase(IServiceProvider serviceProvider)
    {
        _ = serviceProvider;
    }
    
    public Task OnStartAsync(IAcceptor acceptor, IDispatcher dispatcher,CancellationToken stoppingToken)
    {
        var channelHandlerBinder = MessageHandlerBinderProvider.GetHandlerBinder(this.GetType());
        return channelHandlerBinder.BindAndStart(this, dispatcher, stoppingToken);
    }

    public virtual void OnSessionCreated(ISession session)
    {
        
    }

    public virtual void OnSessionClosed(ISession session)
    {
        
    }
    
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Invoked as an instance member by AppMessageHandlerBinderGen.")]
    internal Task StartMessageProcessLoop<TReq,TReply>(IDispatcher dispatcher, Func<MessageContext<TReq>,ValueTask<ResultContext<TReply>>> handler, CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            var currentAwaiter = new HandlerAwaiter<MessageContext<TReq>>();
            dispatcher.AddHandler<TReq>(currentAwaiter.SetResult);
            while (!stoppingToken.IsCancellationRequested)
            {
                var task = new ValueTask<MessageContext<TReq>>(currentAwaiter, currentAwaiter.Version);
                var context = await task;
                currentAwaiter.Reset();
                var resultContext = await handler(context);
                await dispatcher.SendAsync(context.FromSession, resultContext);
            }
            dispatcher.RemoveHandler<TReq>(currentAwaiter.SetResult);
        }, stoppingToken);
    }
    
    internal Task StartMessageProcessLoop<TReq,TReply>(IDispatcher dispatcher, Func<TReq,ValueTask<ResultContext<TReply>>> handler, CancellationToken stoppingToken) 
        => StartMessageProcessLoop<TReq,TReply>(dispatcher, (context) => handler(context.Message), stoppingToken);
    
    internal Task StartMessageProcessLoop<TReq,TReply>(IDispatcher dispatcher, Func<TReq,ISession,ValueTask<ResultContext<TReply>>> handler, CancellationToken stoppingToken) 
        => StartMessageProcessLoop<TReq,TReply>(dispatcher, (context) => handler(context.Message,context.FromSession), stoppingToken);
    
}