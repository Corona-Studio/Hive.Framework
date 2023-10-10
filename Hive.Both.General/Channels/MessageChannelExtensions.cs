using System;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hive.Both.General.Channels
{
    public static class MessageChannelExtensions
    {
        public static IServerMessageChannel<TRead, TWrite> CreateServerChannel<TRead, TWrite>(
            this IDispatcher dispatcher, IServiceProvider serviceProvider)
        {
            return new ServerMessageChannel<TRead, TWrite>(dispatcher,
                serviceProvider.GetRequiredService<ILogger<ServerMessageChannel<TRead, TWrite>>>());
        }
        
        public static IMessageChannel<TRead, TWrite> CreateChannel<TRead, TWrite>(this IDispatcher dispatcher, ISession session, IServiceProvider serviceProvider)
        {
            return new MessageChannel<TRead, TWrite>(session, dispatcher,
                serviceProvider.GetRequiredService<ILogger<MessageChannel<TRead, TWrite>>>());
        }
    }
}