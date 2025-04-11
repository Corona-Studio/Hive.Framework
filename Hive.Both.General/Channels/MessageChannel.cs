using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Channels
{
    public class MessageChannel<TRead, TWrite> : IMessageChannel<TRead, TWrite>
    {
        private readonly Channel<TRead> _channel = Channel.CreateUnbounded<TRead>();
        private readonly IDispatcher _dispatcher;
        private readonly ISession _session;

        public MessageChannel(ISession session, IDispatcher dispatcher)
        {
            _session = session;
            _dispatcher = dispatcher;
            dispatcher.AddHandler<TRead>(OnReceive);
        }

        public ValueTask<TRead> ReadAsync(CancellationToken token = default)
        {
            return _channel.Reader.ReadAsync(token);
        }

        public ValueTask<bool> WriteAsync(TWrite message)
        {
            return _dispatcher.SendAsync(_session, message);
        }

        private void OnReceive(MessageContext<TRead> context)
        {
            if (!_channel.Writer.TryWrite(context.Message))
            {
            }
        }

        ~MessageChannel()
        {
            try
            {
                _dispatcher.RemoveHandler<TRead>(OnReceive);
            }
            catch (ObjectDisposedException)
            {
                // Ignore
            }
        }
    }
}