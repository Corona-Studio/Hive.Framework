using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Channels
{
    public interface IServerMessageChannel<TRead, in TWrite>
    {
        ValueTask<(ISession session, TRead message)> ReadAsync(CancellationToken token = default);
        ValueTask<bool> WriteAsync(ISession session, TWrite message);
    }
}