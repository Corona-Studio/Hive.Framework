using Hive.Both.General.Channels;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions;
using Microsoft.Extensions.Logging;

namespace Hive.Server.Common.Application;

public interface IChannelHandlerBinder
{
    Task BindAndStart(ServerApplicationBase app, IDispatcher dispatcher, ILoggerFactory loggerFactory, CancellationToken stoppingToken);
    
    internal Task StartMessageProcessLoop<TReq,TReply>(IDispatcher dispatcher, ILoggerFactory loggerFactory,Func<SessionId,TReq,ValueTask<TReply>> handler, CancellationToken stoppingToken)
    {
        var channel = dispatcher.CreateServerChannel<TReq, TReply>(loggerFactory);
        return Task.Run(async () =>
        {
            await foreach (var message in channel.GetAsyncEnumerable(stoppingToken))
            {
                var (session, request) = message;
                var reply = await handler(session.Id, request).ConfigureAwait(false);
                await channel.WriteAsync(session, reply);
            }
        }, stoppingToken);
    }
}

public class ChannelHandlerBinder : IChannelHandlerBinder
{
    public Task BindAndStart(ServerApplicationBase appBase, IDispatcher dispatcher, ILoggerFactory loggerFactory, CancellationToken stoppingToken)
    {
        IChannelHandlerBinder binder = this;
        var app = appBase as ServerApplicationBase;
        var taskList = new List<Task>
        {
            /*binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),
            binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),
            binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),
            binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),*/
        };

        return Task.WhenAll(taskList);
    }
}