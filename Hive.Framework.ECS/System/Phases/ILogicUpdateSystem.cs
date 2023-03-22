using Hive.Framework.ECS.Entity;

namespace Hive.Framework.ECS.System.Phases
{
    public interface ILogicUpdateSystem : ISystem
    {
        void OnLogicUpdate(IEntity entity);
    }
}