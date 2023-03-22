#nullable enable
using Hive.Framework.ECS.Compositor;
using Hive.Framework.ECS.Entity;

namespace Hive.Framework.ECS
{
    public interface ICanManageEntity
    {
        void Instantiate<TCompositor>(IEntity? parent = null) where TCompositor : ICompositor;
        void Destroy(IEntity entity);
    }
}