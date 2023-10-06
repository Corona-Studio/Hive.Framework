using System.Net;

namespace Hive.Network.Shared
{
    public sealed class SessionManagerOptions
    {
        public IPEndPoint ListenEndPoint { get; set; } = new(IPAddress.Any, 0);
    }
}