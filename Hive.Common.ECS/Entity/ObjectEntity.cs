using Hive.Common.ECS.Compositor;

namespace Hive.Common.ECS.Entity;

public class ObjectEntity : Entity, IObjectEntity
{
    public ICompositor Compositor { get; init; } = null!;
    public WorldEntity? WorldEntity { get; internal set; }
}