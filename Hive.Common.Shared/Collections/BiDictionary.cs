using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Hive.Common.Shared.Collections
{
    /// <summary>
    ///     双向字典
    /// </summary>
    /// <typeparam name="TK">键类型</typeparam>
    /// <typeparam name="TV">值类型</typeparam>
    public class BiDictionary<TK, TV> where TK : notnull where TV : notnull
    {
        private readonly Dictionary<TK, TV> _kv = [];
        private readonly Dictionary<TV, TK> _vk = [];

        public BiDictionary()
        {
        }

        public BiDictionary(int capacity)
        {
            _kv = [with(capacity)];
            _vk = [with(capacity)];
        }

        public List<TK> Keys => [.. _kv.Keys];

        public List<TV> Values => [.. _vk.Keys];

        public void ForEach(Action<TK, TV> action)
        {
            var keys = _kv.Keys;
            foreach (var key in keys) action(key, _kv[key]);
        }

        public void Add(TK key, TV value)
        {
            if (key == null || value == null || _kv.ContainsKey(key) || _vk.ContainsKey(value)) return;
            _kv.Add(key, value);
            _vk.Add(value, key);
        }

        public TV? GetValueByKey(TK key)
        {
            if (key != null && _kv.TryGetValue(key, out var byKey)) return byKey;

            return default;
        }

        public TK? GetKeyByValue(TV value)
        {
            if (value != null && _vk.TryGetValue(value, out var byValue)) return byValue;
            return default;
        }

        public bool TrySetValue(TK key, TV value)
        {
            if (key == null || value == null || !_kv.TryGetValue(key, out var oldValue) || _vk.ContainsKey(value)) return false;

            _vk.Remove(oldValue);
            _kv[key] = value;
            _vk.Add(value, key);

            return true;
        }

        public void RemoveByKey(TK key)
        {
            if (key == null) return;

            if (!_kv.TryGetValue(key, out var value)) return;

            _kv.Remove(key);
            _vk.Remove(value);
        }

        public void RemoveByValue(TV value)
        {
            if (value == null) return;

            if (!_vk.TryGetValue(value, out var key)) return;

            _kv.Remove(key);
            _vk.Remove(value);
        }

        public void Clear()
        {
            _kv.Clear();
            _vk.Clear();
        }

        public bool ContainsKey(TK key)
        {
            return key != null && _kv.ContainsKey(key);
        }

        public bool ContainsValue(TV value)
        {
            return value != null && _vk.ContainsKey(value);
        }

        public bool Contains(TK key, TV value)
        {
            if (key == null || value == null) return false;
            return _kv.ContainsKey(key) && _vk.ContainsKey(value);
        }

        public bool TryGetValueByKey(TK key, [MaybeNullWhen(false)] out TV value)
        {
            return _kv.TryGetValue(key, out value);
        }

        public bool TryGetKeyByValue(TV value, [MaybeNullWhen(false)] out TK key)
        {
            return _vk.TryGetValue(value, out key);
        }
    }
}