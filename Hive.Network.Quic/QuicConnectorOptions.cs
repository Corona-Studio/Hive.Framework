using System.Net.Quic;

namespace Hive.Network.Quic;

public class QuicConnectorOptions
{
    public QuicClientConnectionOptions? ClientConnectionOptions { get; set; }
}
