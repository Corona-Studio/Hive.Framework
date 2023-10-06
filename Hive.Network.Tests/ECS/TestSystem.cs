using Hive.Common.ECS.Entity;
using Hive.Common.ECS.System.Phases;

namespace Hive.Network.Tests.ECS
{
    public class TestSystem : IAwakeSystem,ILogicUpdateSystem
    {
        public bool EntityFilter(IEntity entity)
        {
            if (entity is ObjectEntity)
            {
                return true;
            }

            return false;
        }

        void ILogicUpdateSystem.OnLogicUpdate(IEntity entity)
        {
            
        }

        void IAwakeSystem.OnAwake(IEntity entity)
        {
            
        }
    }
}