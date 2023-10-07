using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Hive.Network.Shared.Helpers
{
    public static class SocketHelper
    {
        public static void PatchSocket(this Socket socket)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                var SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                socket.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
            }
        }

        public static async ValueTask RawSendTo(this Socket socket, byte[] data, EndPoint to)
        {
            var sentLen = 0;

            // 将分配的 Conv 返回给客户端
            while (sentLen < data.Length)
            {
                var sent = await socket.SendToAsync(
                    new ArraySegment<byte>(data[sentLen..]),
                    SocketFlags.None,
                    to);

                sentLen += sent;
            }
        }
    }
}