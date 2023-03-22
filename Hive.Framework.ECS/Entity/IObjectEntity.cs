using Hive.Framework.ECS.Compositor;

namespace Hive.Framework.ECS.Entity
{
    public interface IObjectEntity
    {
        public ICompositor Compositor { get; }
        
        public WorldEntity WorldEntity { get; }
    }
}