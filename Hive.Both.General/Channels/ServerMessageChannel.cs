using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.Logging;

namespace Hive.Both.General.Channels
{
    public class ServerMessageChannel<TRead, TWrite> : IServerMessageChannel<TRead, TWrite>
    {
        private readonly ILogger<ServerMessageChannel<TRead, TWrite>> _logger;
        private readonly IDispatcher _dispatcher;
        private readonly Channel<(ISession, TRead)> _channel = Channel.CreateUnbounded<(ISession, TRead)>();

        public ServerMessageChannel(IDispatcher dispatcher, ILogger<ServerMessageChannel<TRead, TWrite>> logger)
        {
            _dispatcher = dispatcher;
            _logger = logger;
            _dispatcher.AddHandler<TRead>(OnReceive);
        }
        
        private void OnReceive(IDispatcher dispatcher, ISession session, TRead message)
        {
            if (!_channel.Writer.TryWrite((session, message)))
            {
                _logger.LogWarning("Failed to write message to channel");
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
    }
}