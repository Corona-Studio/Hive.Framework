using Hive.Codec.Shared;
using Hive.Server.Abstractions;
using MemoryPack;

namespace Hive.Server.Cluster.Messages;

[MemoryPackable]
[MessageDefine]
public partial class NodeLogin
{
    public string Signature;

    public string PublicKey;
    
    public List<ServiceKey> Services;
}


[MemoryPackable]
[MessageDefine]
public partial class NodeLoginResp
{
    public string Signature;
    
    public string PublicKey;
    
    public int NodeId;
}