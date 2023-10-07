using Hive.Common.ECS.Entity;

namespace Hive.Common.ECS.System.Phases;

public interface IAwakeSystem : ISystem
{
    void OnAwake(IEntity entity);
}