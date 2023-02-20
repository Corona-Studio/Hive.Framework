using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
public class SigninMessage
{
    [ProtoMember(1)]
    public int Id { get; set; }
}