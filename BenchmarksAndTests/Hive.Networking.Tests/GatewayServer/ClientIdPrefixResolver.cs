using Hive.Framework.Networking.Shared;

namespace Hive.Framework.Networking.Tests.GatewayServer;

public class ClientIdPrefixResolver : AbstractPrefixResolver
{
    public override object Resolve(ReadOnlySpan<byte> data, ref int index)
    {
        var packetIdSpan = GetAndIncrementIndex(data, 16, ref index);
        var sessionId = new Guid(packetIdSpan);

        return sessionId;
    }
}