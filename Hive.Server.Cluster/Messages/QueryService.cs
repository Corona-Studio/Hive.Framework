using Hive.Codec.Shared;
using MemoryPack;

namespace Hive.Server.Cluster.Messages;

[MemoryPackable]
[MessageDefine]
public partial class QueryServiceReq
{
    public string ServiceName = string.Empty;
}


[MemoryPackable]
[MessageDefine]
public partial class QueryServiceResp
{
    public int NodeId;
    public string ServiceName = string.Empty;
    public string ServiceAddress = string.Empty;
    public int ServicePort;
}
