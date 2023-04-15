#nullable enable
using Hive.Framework.ECS.Compositor;

namespace Hive.Framework.ECS.Entity
{
    public class ObjectEntity :  Entity,IObjectEntity
    {
        public ICompositor Compositor { get; init; }
        public WorldEntity? WorldEntity { get; internal set; }
    }
}