using Hive.Framework.ECS.Entity;

namespace Hive.Framework.ECS.System.Phases
{
    public interface IAwakeSystem : ISystem
    {
        void OnAwake(IEntity entity);
    }
}