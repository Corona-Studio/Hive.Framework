using Hive.Common.ECS.Entity;

namespace Hive.Common.ECS.System;

public interface ISystem
{
    bool EntityFilter(IEntity entity)
    {
        return true;
    }

    void Execute(IEntity entity)
    {
    }
}