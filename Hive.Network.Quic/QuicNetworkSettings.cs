using System.Net;

namespace Hive.Network.Quic;

public static class QuicNetworkSettings
{
    public static readonly IPEndPoint FallBackEndPoint = new(IPAddress.Any, 0);
}