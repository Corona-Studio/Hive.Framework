using System.Collections.Generic;
using Hive.Common.ECS.Entity;

namespace Hive.Common.ECS
{
    internal class EntityDepthComparer : IComparer<IEntity>
    {
        public int Compare(IEntity x, IEntity y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (ReferenceEquals(null, y)) return 1;
            if (ReferenceEquals(null, x)) return -1;
            return x.Depth.CompareTo(y.Depth);
        }
    }
}