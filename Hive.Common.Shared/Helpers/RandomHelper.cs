using System;
using System.Collections.Generic;

namespace Hive.Common.Shared.Helpers
{
    public static class RandomHelper
    {
        private static readonly Random Random = new();

        extension<T>(IList<T> list)
        {
            public T RandomSelect()
            {
                return list[Random.Next(list.Count)];
            }
        }

        extension<T>(IReadOnlyList<T> list)
        {
            public T RandomSelect()
            {
                return list[Random.Next(list.Count)];
            }
        }
    }
}