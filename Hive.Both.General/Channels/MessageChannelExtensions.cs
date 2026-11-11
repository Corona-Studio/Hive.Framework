using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Channels
{
    public static class MessageChannelExtensions
    {
        extension(IDispatcher dispatcher)
        {
            public IServerMessageChannel<TRead, TWrite> CreateServerChannel<TRead, TWrite>()
            {
                return new ServerMessageChannel<TRead, TWrite>(dispatcher);
            }

            public IMessageChannel<TRead, TWrite> CreateChannel<TRead, TWrite>(ISession session)
            {
                return new MessageChannel<TRead, TWrite>(session, dispatcher);
            }
        }
    }
}