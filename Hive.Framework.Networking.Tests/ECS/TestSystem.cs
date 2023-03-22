using Hive.Framework.ECS.Entity;
using Hive.Framework.ECS.System.Phases;

namespace Hive.Framework.Networking.Tests.ECS
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