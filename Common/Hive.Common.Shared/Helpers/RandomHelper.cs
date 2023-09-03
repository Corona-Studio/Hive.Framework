using System;
using System.Collections.Generic;

namespace Hive.Framework.Shared.Helpers
{
    public static class RandomHelper
    {
        private static readonly Random Random = new();

        public static T RandomSelect<T>(this IList<T> list)
        {
            return list[Random.Next(list.Count)];
        }

        public static T RandomSelect<T>(this IReadOnlyList<T> list)
        {
            return list[Random.Next(list.Count)];
        }
    }
}
