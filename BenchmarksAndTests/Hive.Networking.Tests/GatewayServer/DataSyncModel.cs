using Hive.DataSync.Shared.Attributes;

namespace Hive.Framework.Networking.Tests.GatewayServer;

[SyncObject(1)]
public partial class DataSyncModel1
{
    [SyncProperty]
    private int _field1;

    [SyncProperty]
    private double _field2;
}

[SyncObject(2)]
public partial class DataSyncModel2
{
    [SyncProperty]
    private int _field1;

    [SyncProperty]
    private double _field2;
}
