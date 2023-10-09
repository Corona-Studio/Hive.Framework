namespace Hive.Server.Cluster;

public class CentralizedNodeServiceOptions
{
    public string ManagerAddress { get; set; } = "0.0.0.0";
    public int ManagerPort { get; set; } = 11452;
    public int HeartBeatInterval { get; set; } = 1000;
}