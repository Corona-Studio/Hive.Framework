using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Dispatchers
{
    public static class DispatcherExtensions
    {
        public static void BindTo(this ISession session, IDispatcher dispatcher)
        {
            session.OnMessageReceived += dispatcher.Dispatch;
        }

        public static void BindTo(this IAcceptor acceptor, IDispatcher dispatcher)
        {
            var handler = new SessionReceivedHandler(dispatcher.Dispatch);
            acceptor.OnSessionCreated += (sender, id, session) => { session.OnMessageReceived += handler; };

            acceptor.OnSessionClosed += (sender, id, session) => { session.OnMessageReceived -= handler; };
        }
    }
}