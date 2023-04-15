using System;
using System.Collections.Generic;

namespace Hive.Framework.Shared.Collections
{
    public class MultiDictionary<TK, TV>: Dictionary<TK, List<TV>>
    {
        public void Add(TK t, TV k)
        {
            TryGetValue(t, out var list);
            if (list == null)
            {
                list = new List<TV>();
                base[t] = list;
            }
            list.Add(k);
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
        /// <param name="t"></param>
        /// <returns></returns>
        public TV[] GetAll(TK t)
        {
            TryGetValue(t, out var list);
            return list == null ? Array.Empty<TV>() : list.ToArray();
        }

        /// <summary>
        /// 返回内部的list
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public new List<TV>? this[TK t]
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
            return list?.Count>0 ? list[0] : default;
        }

        public bool Contains(TK t, TV k)
        {
            TryGetValue(t, out var list);
            return list != null && list.Contains(k);
        }
    }
}