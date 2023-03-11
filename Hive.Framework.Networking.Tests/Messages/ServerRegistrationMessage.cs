using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
public class ServerRegistrationMessage
{
    [ProtoMember(1)]
    public ushort[] PackagesToReceive { get; set; }
}