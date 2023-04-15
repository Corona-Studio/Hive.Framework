using ProtoBuf;

namespace Hive.Framework.Networking.Tests.Messages;

[ProtoContract]
public class ServerRedirectTestMessage1
{
    [ProtoMember(1)]
    public string Content { get; set; }
}