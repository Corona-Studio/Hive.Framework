using System.Collections.ObjectModel;

namespace Hive.Common.ECS.Entity
{
    public interface IEntity
    {
        IECSArch ECSArch { get; }
        string Name { get; set; }
        int Depth { get; }
        ReadOnlyCollection<IEntity> Children { get; }

        public IEntity Parent { get; set; }
        public long InstanceId { get; init; }

    }
}