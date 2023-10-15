using Hive.Both.Messages.C2S;
using Hive.Both.Messages.S2C;
using Hive.Network.Abstractions;

namespace Hive.Server.Common.Application;

public class TestApplication : ServerApplicationBase
{
    public TestApplication(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        
    }
    

    [MessageHandler]
    public ValueTask<SCHeartBeat> HelloHandler(SessionId sessionId, CSHeartBeat request)
    {
        return new ValueTask<SCHeartBeat>(new SCHeartBeat());
    }
}