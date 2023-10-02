using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Network.Abstractions.Session;

public interface IConnector<TSession> where TSession : ISession
{
    ValueTask<TSession> ConnectAsync(IPEndPoint remoteEndPoint, CancellationToken token = default);
}