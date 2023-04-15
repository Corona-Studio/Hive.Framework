using System.Net;

namespace Hive.Framework.Networking.Shared.Helpers
{
    public static class NetworkAddressHelper
    {
        public static IPEndPoint ToIpEndPoint(string addressWithPort)
        {
            var arr = addressWithPort.Split(':');
            var address = arr[0];
            var port = ushort.TryParse(arr[1], out var outPort) ? outPort : 0;

            var ip = IPAddress.Parse(address);
            var endPoint = new IPEndPoint(ip, port);

            return endPoint;
        }
    }
}