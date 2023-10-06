using System.Collections.Generic;
using System.Linq;

namespace Hive.Common.Shared.Collections
{
    public class MultiHashSetDictionary<TK, TV>: Dictionary<TK, HashSet<TV>>
    {
        public void Add(TK key, TV value)
        {
            TryGetValue(key, out var list);
            if (list == null)
            {
                list = new HashSet<TV>();
                base[key] = list;
            }
            list.Add(value);
        }

        public bool Remove(TK t, TV k)
        {
            TryGetValue(t, out var list);
            if (list == null || !list.Remove(k))
                return false;
            
            if (list.Count == 0)
                Remove(t);
            
            return true;
        }

        /// <summary>
        /// 不返回内部的list,copy一份出来
        /// </summary>
        /// <param name="key"></param>
        /// <param name="outList"></param>
        /// <returns>True if it is not empty</returns>
        public bool GetAll(TK key, List<TV> outList)
        {
            TryGetValue(key, out var set);
            outList.Clear();
            
            if (set == null)
            {
                return false;
            }

            outList.AddRange(set);
            return true;
        }

        /// <summary>
        /// 返回内部的list
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public new HashSet<TV>? this[TK t]
        {
            get
            {
                TryGetValue(t, out var list);
                return list;
            }
        }

        public TV? GetOne(TK t)
        {
            TryGetValue(t, out var list);
            return list != null ? list.FirstOrDefault() : default;
        }

        public bool Contains(TK key, TV value)
        {
            TryGetValue(key, out var set);
            return set != null && set.Contains(value);
        }
    }
}