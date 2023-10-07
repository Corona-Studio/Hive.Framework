using Hive.Common.ECS.Entity;

namespace Hive.Common.ECS.System.Phases;

public interface ILogicUpdateSystem : ISystem
{
    void OnLogicUpdate(IEntity entity);
}