using System.IO;

namespace Hive.Network.Abstractions.GatewayServer;

public interface ISecureStreamProvider
{
    Stream GetSecuredStream(Stream stream);
}