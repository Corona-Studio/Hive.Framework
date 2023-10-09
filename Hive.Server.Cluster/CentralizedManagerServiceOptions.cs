namespace Hive.Server.Cluster;

public class CentralizedManagerServiceOptions
{
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int ListenPort { get; set; } = 11452;
}