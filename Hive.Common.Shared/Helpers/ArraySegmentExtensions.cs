using System;

namespace Hive.Common.Shared.Helpers
{
    public static class ArraySegmentExtensions
    {
        public static ArraySegment<T> Shift<T>(this ArraySegment<T> segment, int amount)
        {
            if (segment.Array == null)
                throw new ArgumentNullException(nameof(segment.Array));

            return new ArraySegment<T>(segment.Array, segment.Offset + amount, segment.Count - amount);
        }
    }
}