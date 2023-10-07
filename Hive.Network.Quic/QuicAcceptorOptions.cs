using System.Net.Quic;
using System.Runtime.Versioning;

namespace Hive.Network.Quic;

[RequiresPreviewFeatures]
public class QuicAcceptorOptions
{
    public QuicListenerOptions? QuicListenerOptions { get; set; }
}