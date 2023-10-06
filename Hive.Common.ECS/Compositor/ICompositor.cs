using Hive.Common.ECS.Entity;

namespace Hive.Common.ECS.Compositor
{
    public interface ICompositor
    {
        public ObjectEntity Composite(long id, IEntity parent);
    }
}