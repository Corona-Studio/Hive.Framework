using Hive.Framework.ECS.Component;
using Hive.Framework.ECS.Entity;
using Hive.Framework.ECS.System;

namespace Hive.Framework.ECS
{
    public interface IECSArch
    {
        EntityManager EntityManager { get; }
        ComponentManager ComponentManager { get; }
        SystemManager SystemManager { get; }
    }
}