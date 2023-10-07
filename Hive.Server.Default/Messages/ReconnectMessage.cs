using MemoryPack;
using ProtoBuf;

namespace Hive.Server.Default.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ReconnectMessage
{
}