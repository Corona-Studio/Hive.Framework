using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Dispatchers
{
    public readonly struct MessageContext<T>
    {
        public readonly ISession FromSession;
        public readonly IDispatcher Dispatcher;
        public readonly T Message;

        public MessageContext(ISession fromSession, IDispatcher dispatcher, T message)
        {
            FromSession = fromSession;
            Message = message;
            Dispatcher = dispatcher;
        }
    }
}