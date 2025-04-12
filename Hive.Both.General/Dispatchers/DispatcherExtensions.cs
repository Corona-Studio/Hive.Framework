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
            acceptor.OnSessionCreated += (_, _, session) => { session.OnMessageReceived += dispatcher.Dispatch; };

            acceptor.OnSessionClosed += (_, _, session) => { session.OnMessageReceived -= dispatcher.Dispatch; };
        }
    }
}