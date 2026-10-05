using System;
using System.Threading;

namespace Hive.Common.Shared.Pooling
{
    /// <summary>A bounded cache whose rent/return operations allocate no queue nodes.</summary>
    public sealed class BoundedObjectPool<T> where T : class
    {
        private readonly T?[] _slots;
        private readonly Func<T> _factory;

        public BoundedObjectPool(Func<T> factory, int capacity = 32)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _slots = new T?[capacity];
        }

        public T Rent()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (Volatile.Read(ref _slots[i]) == null) continue;
                var item = Interlocked.Exchange(ref _slots[i], null);
                if (item != null) return item;
            }
            return _factory();
        }

        /// <summary>Return exactly once after the last user relinquishes ownership.</summary>
        public bool TryReturn(T item)
        {
            for (var i = 0; i < _slots.Length; i++)
                if (Interlocked.CompareExchange(ref _slots[i], item, null) == null) return true;
            return false;
        }
    }
}
