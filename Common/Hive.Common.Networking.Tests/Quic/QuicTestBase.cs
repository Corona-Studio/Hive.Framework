using System.Net.Quic;
using System.Runtime.Versioning;
using Hive.Framework.Networking.Quic;

namespace Hive.Framework.Networking.Tests.Quic;

[RequiresPreviewFeatures]
public abstract class QuicTestBase : 
    AbstractNetworkingTestBase<QuicSession<ushort>, QuicConnection, QuicAcceptor<ushort, Guid>, FakeQuicClientManager>
{
}