using System.Net;

namespace Hive.Server.Shared;

public class ClientServiceOptions
{
    public int HeartBeatTimeout { get; set; } = 10_000;
    public IPEndPoint EndPoint { get; set; } = new(IPAddress.Any, 0);
}