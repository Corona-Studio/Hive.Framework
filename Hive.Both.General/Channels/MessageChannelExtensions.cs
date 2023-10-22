using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Channels
{
    public static class MessageChannelExtensions
    {
        public static IServerMessageChannel<TRead, TWrite> CreateServerChannel<TRead, TWrite>(
            this IDispatcher dispatcher)
        {
            return new ServerMessageChannel<TRead, TWrite>(dispatcher);
        }
        
        public static IMessageChannel<TRead, TWrite> CreateChannel<TRead, TWrite>(this IDispatcher dispatcher, ISession session)
        {
            return new MessageChannel<TRead, TWrite>(session, dispatcher);
        }
    }
}