using Hive.Framework.Codec.Abstractions;

namespace Hive.Framework.Networking.Tests.GatewayServer;

public class ClientIdPrefixResolver : IPacketPrefixResolver
{
    public object Resolve(ReadOnlySpan<byte> data, ref int index)
    {
        var packetIdSpan = data[index..(index + 16)];
        var sessionId = new Guid(packetIdSpan);

        index += 16;

        return sessionId;
    }
}