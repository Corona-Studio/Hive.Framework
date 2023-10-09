using Hive.Codec.Shared;
using Hive.Server.Abstractions;
using MemoryPack;

namespace Hive.Server.Cluster.Messages;

[MemoryPackable]
[MessageDefine]
public partial class NodeLoginReq
{
    public string Signature;

    public string PublicKey;
    
    public string MachineId;
    
    public List<ServiceKey> Services;
}


[MemoryPackable]
[MessageDefine]
public partial class NodeLoginResp
{
    public ErrorCode ErrorCode;
    
    public string Signature;
    
    public string PublicKey;
    
    public int NodeId;

    public NodeLoginResp(ErrorCode errorCode, string signature, string publicKey)
    {
        ErrorCode = errorCode;
        Signature = signature;
        PublicKey = publicKey;
    }
}