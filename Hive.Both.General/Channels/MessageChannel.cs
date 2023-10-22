using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Channels
{
    public class MessageChannel<TRead, TWrite> : IMessageChannel<TRead,TWrite>
    {
        private readonly ISession _session;
        private readonly IDispatcher _dispatcher;
        private readonly Channel<TRead> _channel = Channel.CreateUnbounded<TRead>();

        public MessageChannel(ISession session, IDispatcher dispatcher)
        {
            _session = session;
            _dispatcher = dispatcher;
            dispatcher.AddHandler<TRead>(OnReceive);
        }

        private void OnReceive(MessageContext<TRead> context)
        {
            if (!_channel.Writer.TryWrite(context.Message))
            {
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
        
        ~MessageChannel()
        {
            _dispatcher.RemoveHandler<TRead>(OnReceive);
        }
    }
}