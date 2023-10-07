using Hive.Common.ECS.Component;
using Hive.Common.ECS.Entity;
using Hive.Common.ECS.System;

namespace Hive.Common.ECS;

public interface IECSArch
{
    EntityManager EntityManager { get; }
    ComponentManager ComponentManager { get; }
    SystemManager SystemManager { get; }
}