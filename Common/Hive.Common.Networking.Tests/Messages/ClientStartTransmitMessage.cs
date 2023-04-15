using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
public class ClientStartTransmitMessage
{
    [ProtoMember(1)]
    public ushort[] ExcludeRedirectPacketIds { get; set; }
}