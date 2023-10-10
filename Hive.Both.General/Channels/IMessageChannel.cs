using System.Threading;
using System.Threading.Tasks;

namespace Hive.Both.General.Channels
{
    public interface IMessageChannel<TRead, in TWrite>
    {
        ValueTask<TRead> ReadAsync(CancellationToken token = default);
        ValueTask<bool> WriteAsync(TWrite message);
    }
}