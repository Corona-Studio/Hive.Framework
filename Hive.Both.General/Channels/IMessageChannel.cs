using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Both.General.Channels
{
    public interface IMessageChannel
    {
        ValueTask<object?> ReadAsync(CancellationToken token = default);
        ValueTask<bool> WriteAsync(object message);
    }
    
    public interface IMessageChannel<TRead, in TWrite>: IMessageChannel
    {
        new ValueTask<TRead> ReadAsync(CancellationToken token = default);
        ValueTask<bool> WriteAsync(TWrite message);

        async ValueTask<object?> IMessageChannel.ReadAsync(CancellationToken token)
        {
            return await ReadAsync(token) ?? default;
        }
        
        async IAsyncEnumerable<TRead> GetAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                yield return await ReadAsync(cancellationToken);
            }
        }

        ValueTask<bool> IMessageChannel.WriteAsync(object message)
        {
            return WriteAsync((TWrite) message);
        }
    }
}