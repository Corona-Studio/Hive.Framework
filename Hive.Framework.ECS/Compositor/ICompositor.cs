using Hive.Framework.ECS.Entity;

namespace Hive.Framework.ECS.Compositor
{
    public interface ICompositor
    {
        public ObjectEntity Composite(int id, IEntity parent);
    }
}