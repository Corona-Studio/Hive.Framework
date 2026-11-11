using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Dispatchers
{
    public static class DispatcherExtensions
    {
        extension(ISession session)
        {
            public void BindTo(IDispatcher dispatcher)
            {
                session.OnMessageReceived += dispatcher.Dispatch;
            }
        }

        extension(IAcceptor acceptor)
        {
            public void BindTo(IDispatcher dispatcher)
            {
                acceptor.OnSessionCreated += (_, _, session) => { session.OnMessageReceived += dispatcher.Dispatch; };

                acceptor.OnSessionClosed += (_, _, session) => { session.OnMessageReceived -= dispatcher.Dispatch; };
            }
        }
    }
}