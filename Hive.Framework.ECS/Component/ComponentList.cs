using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Hive.Framework.Shared.Collections;

namespace Hive.Framework.ECS.Component
{
    public class ComponentList<T> : IEnumerable<T> where T : IEntityComponent
    {
        private readonly BiDictionary<int, int> _entityIdToComponentIndex = new();

        private readonly T _defaultT = default!;

        private const int DefaultCapacity = 4;
        private T[] _items;
        private int _size = 0;

        private readonly ReaderWriterLockSlim _rwLock = new();
        
#pragma warning disable CA1825
        // ReSharper disable once UseArrayEmptyMethod
        private static readonly T[] s_emptyArray = new T[0];
        private int _version = 1;
#pragma warning restore CA1825

        private readonly List<T> _components = new();
        private IReadOnlyList<T> Components { get; }

        public ComponentList()
        {
            Components = new ReadOnlyCollection<T>(_items);
        }

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

        public int Capacity
        {
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
                    _items = s_emptyArray;
                }
            }
        }

        private void Append(T item)
        {
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
        }

        public void AttachToEntity(int entityId, T component)
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
                Append(component);
                _entityIdToComponentIndex.Add(entityId, idx);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public void DetachFromEntity(int entity)
        {
            lock (_entityIdToComponentIndex)
            {
                if (!_entityIdToComponentIndex.TryGetValueByKey(entity, out var idx)) return;

                lock (_items)
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
            }
        }

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

        public ref T GetRefByEntityId(int entityId)
        {
            _rwLock.EnterReadLock();

            try
            {
                if (_entityIdToComponentIndex.TryGetValueByKey(entityId, out var idx))
                {
                    // Extracting 'throw' statement into a different
                    // method helps the jitter to inline a property access.
                    if ((uint)idx >= (uint)_items.Length)
                        throw new IndexOutOfRangeException(nameof(idx));

                    return ref _items[idx];
                }

                throw new ArgumentOutOfRangeException(nameof(entityId));
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        public void Modify(int entityId, RefAction<T> modifier)
        {
            ref var item = ref GetRefByEntityId(entityId);
            lock (item)
            {
                // todo _entityIdToComponentIndex and _items can not be written in this scope.
                modifier(ref item);
            }
        }


        public IEnumerator<T> GetEnumerator()
        {
            return Components.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Components.GetEnumerator();
        }
    }
}