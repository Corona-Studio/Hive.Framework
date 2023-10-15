using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Channels
{
    public interface IServerMessageChannel
    {
        ValueTask<(ISession session, object? message)> ReadAsync(CancellationToken token = default);
        ValueTask<bool> WriteAsync(ISession session, object message);
    }
    
    public interface IServerMessageChannel<TRead, in TWrite> : IServerMessageChannel
    {
        new ValueTask<(ISession session, TRead message)> ReadAsync(CancellationToken token = default);
        ValueTask<bool> WriteAsync(ISession session, TWrite message);

        async ValueTask<(ISession session, object? message)> IServerMessageChannel.ReadAsync(CancellationToken token)
        {
            return await ReadAsync(token);
        }
        
        async IAsyncEnumerable<(ISession session,TRead message)> GetAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                yield return await ReadAsync(cancellationToken);
            }
        }

        ValueTask<bool> IServerMessageChannel.WriteAsync(ISession session, object message)
        {
            return WriteAsync(session, (TWrite) message);
        }
    }
}