using Hive.Framework.ECS.Entity;

namespace Hive.Framework.ECS.System
{
    public interface ISystem
    {
        bool EntityFilter(IEntity entity)
        {
            return true;
        }

        void Execute(IEntity entity)
        {
            
        }
    }
}