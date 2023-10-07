using Hive.Common.ECS.Compositor;

namespace Hive.Common.ECS.Entity;

public interface IObjectEntity
{
    public ICompositor Compositor { get; }

    public WorldEntity WorldEntity { get; }
}