using Hive.Framework.ECS.Entity;

namespace Hive.Framework.ECS.Compositor
{
    public interface ICompositor
    {
        public ObjectEntity Composite(long id, IEntity parent);
    }
}