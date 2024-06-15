using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Channels
{
    public class ServerMessageChannel<TRead, TWrite> : IServerMessageChannel<TRead, TWrite>
    {
        private readonly Channel<(ISession, TRead)> _channel = Channel.CreateUnbounded<(ISession, TRead)>();
        private readonly IDispatcher _dispatcher;

        public ServerMessageChannel(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _dispatcher.AddHandler<TRead>(OnReceive);
        }

        public ValueTask<(ISession session, TRead message)> ReadAsync(CancellationToken token = default)
        {
            return _channel.Reader.ReadAsync(token);
        }

        public ValueTask<bool> WriteAsync(ISession session, TWrite message)
        {
            return _dispatcher.SendAsync(session, message);
        }

        private void OnReceive(MessageContext<TRead> context)
        {
            if (!_channel.Writer.TryWrite((context.FromSession, context.Message)))
            {
            }
        }

        ~ServerMessageChannel()
        {
            _dispatcher.RemoveHandler<TRead>(OnReceive);
        }
    }
}