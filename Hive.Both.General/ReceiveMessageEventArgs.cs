using Hive.Network.Abstractions.Session;

namespace Hive.Both.General
{
    public struct ReceiveMessageEventArgs<T>
    {
        public readonly ISession Session;
        public readonly T Message;

        public ReceiveMessageEventArgs(ISession session, T message)
        {
            Session = session;
            Message = message;
        }
    }
}