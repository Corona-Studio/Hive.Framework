using System.Threading.Tasks.Sources;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;

namespace Hive.Server.Common.Application;

public interface IMessageHandlerBinder
{
    Task BindAndStart(ServerApplicationBase app, IDispatcher dispatcher, CancellationToken stoppingToken);
}

internal class HandlerAwaiter<T> : IValueTaskSource<T>, IValueTaskSource
{
    private ManualResetValueTaskSourceCore<T> _core = new();

    public short Version => _core.Version;
    
    public void Reset()
    {
        _core.Reset();
    }

    public HandlerAwaiter()
    {
        _core.RunContinuationsAsynchronously = true;
    }
    
    internal void SetResult(T result)
    {
        _core.SetResult(result);
    }

    public T GetResult(short token)
    {
        return _core.GetResult(token);
    }

    void IValueTaskSource.GetResult(short token)
    {
        _core.GetResult(token);
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _core.GetStatus(token);
    }

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _core.OnCompleted(continuation, state, token, flags);
    }
}

public class MessageHandlerBinder : IMessageHandlerBinder
{
    public Task BindAndStart(ServerApplicationBase appBase, IDispatcher dispatcher, CancellationToken stoppingToken)
    {
        IMessageHandlerBinder binder = this;
        var app = appBase as ServerApplicationBase;
        var taskList = new List<Task>
        {
            //appBase.StartMessageProcessLoop<CSHeartBeat, SCHeartBeat>(dispatcher, app.HelloHandler, stoppingToken),
            /*binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),
            binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),
            binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),
            binder.StartMessageProcessLoop(dispatcher, loggerFactory, app.xxx, stoppingToken),*/
        };

        return Task.WhenAll(taskList);
    }
}