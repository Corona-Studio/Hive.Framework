using Hive.Framework.ECS.Entity;

namespace Hive.Framework.ECS.System.Phases
{
    public interface IFrameUpdateSystem : ISystem
    {
        void OnFrameUpdate(IEntity entity);
    }
}