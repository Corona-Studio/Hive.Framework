using System.Net.Sockets;
using Hive.Framework.Networking.Tcp;

namespace Hive.Framework.Networking.Tests.BasicNetworking.Tcp;

public abstract class TcpTestBase : 
    AbstractNetworkingTestBase<TcpSession<ushort>, Socket, TcpAcceptor<ushort, Guid>, FakeTcpClientManager>
{
}