using System;

namespace Hive.Common.Shared.Helpers
{
    public static class ArraySegmentExtensions
    {
        extension<T>(ArraySegment<T> segment)
        {
            public ArraySegment<T> Shift(int amount)
            {
                if (segment.Array == null)
                    throw new ArgumentNullException(nameof(segment.Array));

                return new ArraySegment<T>(segment.Array, segment.Offset + amount, segment.Count - amount);
            }
        }
    }
}