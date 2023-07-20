using System.Net.Sockets;
using Hive.Framework.Networking.Udp;

namespace Hive.Framework.Networking.Tests.Udp;

public abstract class UdpTestBase :
    AbstractNetworkingTestBase<UdpSession<ushort>, Socket, UdpAcceptor<ushort, Guid>, FakeUdpClientManager>
{
}