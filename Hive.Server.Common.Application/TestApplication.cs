using Hive.Both.General.Dispatchers;
using Hive.Both.Messages.C2S;
using Hive.Both.Messages.S2C;

namespace Hive.Server.Common.Application;

public class TestApplication : ServerApplicationBase
{
    public TestApplication(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        
    }
    
    
    [MessageHandler]
    
    public async ValueTask<ResultContext<SCHeartBeat>> HelloHandler(CSHeartBeat request)
    {
        return new ResultContext<SCHeartBeat>(new SCHeartBeat());
    }
}