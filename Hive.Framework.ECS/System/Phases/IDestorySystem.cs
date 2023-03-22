using Hive.Framework.ECS.Entity;

namespace Hive.Framework.ECS.System.Phases
{
    public interface IDestroySystem : ISystem
    {
        void OnDestroy(IEntity entity);
    }
}