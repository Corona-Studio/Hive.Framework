using Hive.Common.ECS.Entity;

namespace Hive.Common.ECS.System.Phases;

public interface IFrameUpdateSystem : ISystem
{
    void OnFrameUpdate(IEntity entity);
}