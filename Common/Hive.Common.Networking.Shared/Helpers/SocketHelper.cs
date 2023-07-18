using System.Net.Sockets;
using System;
using System.Runtime.InteropServices;

namespace Hive.Framework.Networking.Shared.Helpers;

public static class SocketHelper
{
    public static void PatchSocket(this Socket socket)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            var SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            socket.IOControl((int)SIO_UDP_CONNRESET, new [] { Convert.ToByte(false) }, null);
        }
    }
}