using Hive.Common.ECS.Compositor;
using Hive.Common.ECS.Entity;

namespace Hive.Common.ECS;

public interface ICanManageEntity
{
    void Instantiate<TCompositor>(IEntity? parent = null) where TCompositor : ICompositor;
    void Destroy(IEntity entity);
}