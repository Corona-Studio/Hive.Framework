using System.Collections.Concurrent;
using System;

namespace Hive.Framework.Shared
{
    public class ObjectPool<T>
    {
        private readonly ConcurrentBag<T> _objects;
        private readonly Func<T> _objectGenerator;
        private readonly Action<T>? _returnOp;

        public ObjectPool(Func<T> objectGenerator, Action<T>? returnOp = null)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _objects = new ConcurrentBag<T>();
            _returnOp = returnOp;
        }

        public T Get()
        {
            return _objects.TryTake(out var item) ? item : _objectGenerator();
        }

        public void Return(T item)
        {
            _returnOp?.Invoke(item);
            _objects.Add(item);
        }
    }
}