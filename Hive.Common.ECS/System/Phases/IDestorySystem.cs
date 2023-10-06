using Hive.Common.ECS.Entity;

namespace Hive.Common.ECS.System.Phases
{
    public interface IDestroySystem : ISystem
    {
        void OnDestroy(IEntity entity);
    }
}