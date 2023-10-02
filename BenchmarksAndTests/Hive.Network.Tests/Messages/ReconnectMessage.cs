using MemoryPack;
using ProtoBuf;

namespace Hive.Network.Tests.Messages;

[ProtoContract]
[MemoryPackable]
public partial class ReconnectMessage { }