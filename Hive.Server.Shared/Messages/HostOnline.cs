using MemoryPack;

namespace Hive.Server.Shared.Messages;

[MemoryPackable]
public partial class HostOnline
{
    public List<ServiceInfo> Services = new();
}

[MemoryPackable]
public partial struct ServiceInfo
{
    public string ServiceName;
}