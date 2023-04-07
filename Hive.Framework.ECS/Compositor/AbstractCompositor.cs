using Hive.Framework.ECS.Entity;

namespace Hive.Framework.ECS.Compositor
{
    public abstract class AbstractCompositor<T> : ICompositor where T: ObjectEntity,new()
    {
        ObjectEntity ICompositor.Composite(long id, IEntity parent)
        {
            var worldEntity = parent switch
            {
                WorldEntity entity => entity,
                ObjectEntity parentObjectEntity => parentObjectEntity.WorldEntity,
                _ => null
            };


            var objectEntity = new T
            {
                InstanceId = id,
                Compositor = this,
                WorldEntity = worldEntity,
                Parent = parent,
            };
            
            Composite(objectEntity);
            return objectEntity;
        }

        protected abstract void Composite(T entity);
    }
}