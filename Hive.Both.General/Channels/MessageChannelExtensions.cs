using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.Logging;

namespace Hive.Both.General.Channels
{
    public static class MessageChannelExtensions
    {
        public static IServerMessageChannel<TRead, TWrite> CreateServerChannel<TRead, TWrite>(
            this IDispatcher dispatcher, ILoggerFactory loggerFactory)
        {
            return new ServerMessageChannel<TRead, TWrite>(dispatcher);
        }
        
        public static IMessageChannel<TRead, TWrite> CreateChannel<TRead, TWrite>(this IDispatcher dispatcher, ISession session, ILoggerFactory loggerFactory)
        {
            return new MessageChannel<TRead, TWrite>(session, dispatcher);
        }
    }
}