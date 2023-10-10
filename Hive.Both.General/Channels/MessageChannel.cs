using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;
using Microsoft.Extensions.Logging;

namespace Hive.Both.General.Channels
{
    public class MessageChannel<TRead, TWrite> : IMessageChannel<TRead,TWrite>
    {
        private readonly ISession _session;
        private readonly IDispatcher _dispatcher;
        private readonly ILogger<MessageChannel<TRead, TWrite>> _logger;
        private readonly Channel<TRead> _channel = Channel.CreateUnbounded<TRead>();
        public MessageChannel(ISession session, IDispatcher dispatcher, ILogger<MessageChannel<TRead, TWrite>> logger)
        {
            _session = session;
            _dispatcher = dispatcher;
            _logger = logger;
            dispatcher.AddHandler<TRead>(OnReceive);
        }

        private void OnReceive(IDispatcher dispatcher, ISession session, TRead message)
        {
            if (!_channel.Writer.TryWrite( message))
            {
                _logger.LogWarning("Failed to write message to channel");
            }
        }

        public ValueTask<TRead> ReadAsync(CancellationToken token = default)
        {
            return _channel.Reader.ReadAsync(token);
        }

        public ValueTask<bool> WriteAsync(TWrite message)
        {
            return _dispatcher.SendAsync(_session, message);
        }
    }
}