using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Hive.Framework.Shared.Collections;

namespace Hive.Framework.ECS.Component
{
    public class ComponentList<T> where T : IEntityComponent
    {
        // NOTICE: 操作_entityIdToComponentIndex和_items，必须调用_rwLock的EnterWriteLock
        private readonly BiDictionary<long, int> _entityIdToComponentIndex = new();
        private T[] _items = SEmptyArray;
        
        private const int DefaultCapacity = 4;
        private int _size;

        private readonly ReaderWriterLockSlim _rwLock = new();

#pragma warning disable CA1825
        // ReSharper disable once UseArrayEmptyMethod
        private static readonly T[] SEmptyArray = new T[0];
        private int _version = 1;
#pragma warning restore CA1825
        

        #region List Implement

        /// <summary>
        /// Ensures that the capacity of this list is at least the specified <paramref name="capacity"/>.
        /// If the current capacity of the list is less than specified <paramref name="capacity"/>,
        /// the capacity is increased by continuously twice current capacity until it is at least the specified <paramref name="capacity"/>.
        /// </summary>
        /// <param name="capacity">The minimum capacity to ensure.</param>
        /// <returns>The new capacity of this list.</returns>
        public int EnsureCapacity(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (_items.Length >= capacity) return _items.Length;
            Grow(capacity);
            _version++;

            return _items.Length;
        }

        /// <summary>
        /// Increase the capacity of this list to at least the specified <paramref name="capacity"/>.
        /// </summary>
        /// <param name="capacity">The minimum capacity to ensure.</param>
        private void Grow(int capacity)
        {
            Debug.Assert(_items.Length < capacity);

            int newcapacity = _items.Length == 0 ? DefaultCapacity : 2 * _items.Length;

            // Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
            // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
            if ((uint)newcapacity > Array.MaxLength) newcapacity = Array.MaxLength;

            // If the computed capacity is still less than specified, set to the original argument.
            // Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
            if (newcapacity < capacity) newcapacity = capacity;

            Capacity = newcapacity;
        }

        private int Capacity
        {
            // ReSharper disable once UnusedMember.Local
            get => _items.Length;
            set
            {
                if (value < _size)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                if (value == _items.Length) return;

                if (value > 0)
                {
                    var newItems = new T[value];
                    if (_size > 0)
                    {
                        Array.Copy(_items, newItems, _size);
                    }

                    _items = newItems;
                }
                else
                {
                    _items = SEmptyArray;
                }
            }
        }

        private void AppendUnsafe(T item)
        {
            _version++;
            T[] array = _items;
            int size = _size;
            if ((uint)size < (uint)array.Length)
            {
                _size = size + 1;
                array[size] = item;
            }
            else
            {
                AddWithResize(item);
            }
        }

        // Non-inline from List.Add to improve its code quality as uncommon path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddWithResize(T item)
        {
            Debug.Assert(_size == _items.Length);
            var size = _size;
            Grow(size + 1);
            _size = size + 1;
            _items[size] = item;
        }

        #endregion

        private void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_size)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            _size--;
            if (index < _size)
            {
                Array.Copy(_items, index + 1, _items, index, _size - index);
            }

            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                _items[_size] = default!;
            }
            
            _version++;
        }

        public void AttachToEntity(long entityId, T component)
        {
            _rwLock.EnterWriteLock();

            try
            {
                if (_entityIdToComponentIndex.ContainsKey(entityId))
                {
                    // todo log
                    return;
                }

                var idx = _size;
                AppendUnsafe(component);
                _entityIdToComponentIndex.Add(entityId, idx);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public void DetachFromEntity(long entity)
        {
            _rwLock.EnterWriteLock();
            if (!_entityIdToComponentIndex.TryGetValueByKey(entity, out var idx)) return;

            try
            {
                var lastIdx = _size - 1;
                _entityIdToComponentIndex.RemoveByKey(entity);
                if (idx != lastIdx)
                {
                    _items[idx] = _items[lastIdx];
                    if (_entityIdToComponentIndex.TryGetKeyByValue(lastIdx, out var lastEntityId))
                    {
                        _entityIdToComponentIndex.TrySetValue(lastEntityId, idx);
                    }
                }

                RemoveAt(lastIdx);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Get the reference of a component in this list.
        /// </summary>
        /// <param name="idx"></param>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public ref readonly T this[int idx]
        {
            get
            {
                // Extracting 'throw' statement into a different
                // method helps the jitter to inline a property access.
                if ((uint)idx >= (uint)_items.Length)
                    throw new IndexOutOfRangeException(nameof(idx));

                return ref _items[idx];
            }
        }
        
        /// <summary>
        /// Get the reference of a component by belonged entity id.
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns>A reference to the component</returns>
        public ref T GetRefByEntityId(long entityId)
        {
            _rwLock.EnterReadLock();

            try
            {
                return ref GetRefByEntityIdUnsafe(entityId);
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        private ref T GetRefByEntityIdUnsafe(long entityId)
        {
            if (_entityIdToComponentIndex.TryGetValueByKey(entityId, out var idx))
            {
                // Extracting 'throw' statement into a different
                // method helps the jitter to inline a property access.
                if ((uint)idx >= (uint)_items.Length)
                    throw new IndexOutOfRangeException(nameof(idx));

                {
                    return ref _items[idx];
                }
            }

            return ref Unsafe.NullRef<T>();
        }

        public void Update(long entityId, RefAction<T> modifier)
        {
            _rwLock.EnterReadLock();
            ref var component = ref Unsafe.NullRef<T>();
            try
            {
                component = ref GetRefByEntityIdUnsafe(entityId);
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
            
            if (Unsafe.IsNullRef(ref component))
                return;
            
            // todo 也许有更好的方法
            lock (this)
            {
                modifier(ref component);
            }
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        // ReSharper disable once RedundantExtendsListEntry
        public struct Enumerator : IEnumerator<T>, IEnumerator
        {
            private readonly ComponentList<T> _list;
            private int _index;
            private readonly int _version;
            private T? _current;

            internal Enumerator(ComponentList<T> list)
            {
                _list = list;
                _index = 0;
                _version = list._version;
                _current = default;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                var localList = _list;

                if (_version == localList._version && ((uint)_index < (uint)localList._size))
                {
                    _current = localList._items[_index];
                    _index++;
                    return true;
                }
                return MoveNextRare();
            }

            private bool MoveNextRare()
            {
                if (_version != _list._version)
                {
                    throw new InvalidOperationException("The list's version has changed.");
                }

                _index = _list._size + 1;
                _current = default;
                return false;
            }

            public T Current => _current!;

            object? IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || _index == _list._size + 1)
                    {
                        throw new InvalidOperationException("There is no more element.");
                    }
                    return Current;
                }
            }

            void IEnumerator.Reset()
            {
                if (_version != _list._version)
                {
                    throw new InvalidOperationException("");
                }

                _index = 0;
                _current = default;
            }
        }
    }
}