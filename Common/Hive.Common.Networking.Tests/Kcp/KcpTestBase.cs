using System.Net.Sockets;
using Hive.Framework.Networking.Kcp;

namespace Hive.Framework.Networking.Tests.Kcp;

public abstract class KcpTestBase :
    AbstractNetworkingTestBase<KcpSession<ushort>, Socket, KcpAcceptor<ushort, Guid>, FakeKcpClientManager>
{
}