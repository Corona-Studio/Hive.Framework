using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;

namespace Hive.Network.Shared.LoadBalancers
{
    public class BasicLoadBalancer<TSession> : ILoadBalancer<TSession> where TSession : ISession
    {
        private readonly ConcurrentDictionary<TSession, bool> _sessionAvailabilityDic = new();
        private readonly List<TSession> _sessions = new();
        private int _currentIndex;

        public int Available
        {
            get
            {
                lock (_sessions)
                {
                    return _sessions.Count;
                }
            }
        }

        public void AddSession(TSession sessionInfo)
        {
            lock (_sessions)
            {
                if (_sessions.Contains(sessionInfo))
                {
                    _sessionAvailabilityDic[sessionInfo] = true;
                    return;
                }

                _sessions.Add(sessionInfo);
                _sessionAvailabilityDic[sessionInfo] = true;
            }
        }

        public bool RemoveSession(TSession sessionInfo)
        {
            _sessionAvailabilityDic.TryRemove(sessionInfo, out _);
            lock (_sessions)
            {
                return _sessions.Remove(sessionInfo);
            }
        }

        public bool UpdateSessionAvailability(TSession sessionInfo, bool available)
        {
            return _sessionAvailabilityDic.TryGetValue(sessionInfo, out var oldAvailability) &&
                   _sessionAvailabilityDic.TryUpdate(sessionInfo, available, oldAvailability);
        }

        public TSession? GetOne()
        {
            lock (_sessions)
            {
                TSession? result;

                while (true)
                {
                    result = _sessions[_currentIndex];
                    _currentIndex = (_currentIndex + 1) % _sessions.Count;

                    if (_sessionAvailabilityDic[result] || _currentIndex == 0) break;
                }

                return result;
            }
        }

        public IReadOnlyList<TSession> GetRawList()
        {
            lock (_sessions)
            {
                return new List<TSession>(_sessions).AsReadOnly();
            }
        }

        public IEnumerator<TSession> GetEnumerator()
        {
            var sessions =
                _sessionAvailabilityDic
                    .Where(x => x.Value)
                    .Select(x => x.Key)
                    .ToList();
            var collection = new ReadOnlyCollection<TSession>(sessions);

            return collection.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}