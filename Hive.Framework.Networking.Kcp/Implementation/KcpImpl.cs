using System.Buffers;
using System.Net.Sockets.Kcp;

namespace Hive.Framework.Networking.Kcp.Implementation
{
    public class KcpImpl : IKcpCallback
    {
        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
        }
    }
}