using System.Net;
using Hive.Codec.Shared;
using MemoryPack;

namespace Hive.Server.Cluster.Messages;

[MemoryPackable]
[MessageDefine]
public partial class QueryServiceReq
{
    public string ServiceName;
}


[MemoryPackable]
[MessageDefine]
public partial class QueryServiceResp
{
    public int NodeId;
    public string ServiceName;
    public string ServiceAddress;
    public int ServicePort;
}
