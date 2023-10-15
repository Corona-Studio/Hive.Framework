using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Channels
{
    public class ServerMessageChannel<TRead, TWrite> : IServerMessageChannel<TRead, TWrite>
    {
        private readonly IDispatcher _dispatcher;
        private readonly Channel<(ISession, TRead)> _channel = Channel.CreateUnbounded<(ISession, TRead)>();

        public ServerMessageChannel(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _dispatcher.AddHandler<TRead>(OnReceive);
        }
        
        private void OnReceive(IDispatcher dispatcher, ISession session, TRead message)
        {
            if (!_channel.Writer.TryWrite((session, message)))
            {
                
            }
        }

        public ValueTask<(ISession session, TRead message)> ReadAsync(CancellationToken token = default)
        {
            return _channel.Reader.ReadAsync(token);
        }

        public ValueTask<bool> WriteAsync(ISession session, TWrite message)
        {
            return _dispatcher.SendAsync(session, message);
        }
        
        ~ServerMessageChannel()
        {
            _dispatcher.RemoveHandler<TRead>(OnReceive);
        }
    }
}