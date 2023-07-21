using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Framework.Networking.Abstractions;
using Hive.Framework.Networking.Shared.Helpers;

namespace Hive.Framework.Networking.Shared
{
    public class DefaultDataDispatcher<TSender> : IDataDispatcher<TSender> where TSender : ISession<TSender>
    {
        public ConcurrentDictionary<Type, IDataDispatcher<TSender>.CallbackWarp> CallbackDictionary { get; } = new();

        public Channel<(Type, TSender, object)> DataDispatchChannel { get; } =
            Channel.CreateBounded<(Type, TSender, object)>(new BoundedChannelOptions(1024)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });

        public async Task StartDispatchLoop()
        {
            while (true)
            {
                if (await DataDispatchChannel.Reader.WaitToReadAsync())
                {
                    var (type, sender, data) = await DataDispatchChannel.Reader.ReadAsync();
                    CallbackDictionary[type].InvokeAll(data, sender);

                    await Task.Delay(1);
                }
            }
        }

        public DefaultDataDispatcher()
        {
            TaskHelper.ManagedRun(StartDispatchLoop, CancellationToken.None);
        }
    }
}