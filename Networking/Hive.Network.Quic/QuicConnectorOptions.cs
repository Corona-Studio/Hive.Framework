using System.Net.Quic;
using System.Runtime.Versioning;

namespace Hive.Network.Quic;

[RequiresPreviewFeatures]
public class QuicConnectorOptions
{
    public QuicClientConnectionOptions? ClientConnectionOptions { get; set; }
}