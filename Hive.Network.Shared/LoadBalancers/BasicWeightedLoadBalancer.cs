using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;

namespace Hive.Network.Shared.LoadBalancers
{
    public class BasicWeightedLoadBalancer<TSession> : ILoadBalancer<TSession> where TSession : ISession
    {
        private readonly Random _random = new();
        private readonly ConcurrentDictionary<TSession, bool> _sessionAvailabilityDic = new();
        private readonly Dictionary<TSession, ushort> _sessions = new();

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
                if (_sessions.ContainsKey(sessionInfo))
                {
                    _sessionAvailabilityDic[sessionInfo] = true;
                    return;
                }

                _sessions.Add(sessionInfo, 0);
                _sessionAvailabilityDic[sessionInfo] = true;
            }
        }

        public bool RemoveSession(TSession session)
        {
            lock (_sessions)
            {
                var removed = _sessions.Remove(session, out _);

                return removed;
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
                var randomWeight = _random.Next(0, _sessions.Sum(p => p.Value));

                foreach (var (session, weight) in _sessions)
                {
                    if (randomWeight < weight && _sessionAvailabilityDic[session]) return session;

                    randomWeight -= weight;
                }

                return _sessions.FirstOrDefault(p => _sessionAvailabilityDic[p.Key]).Key;
            }
        }

        public IReadOnlyList<TSession> GetRawList()
        {
            lock (_sessions)
            {
                return _sessions.Select(p => p.Key).ToList().AsReadOnly();
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

        public bool UpdateWeight(TSession session, ushort weight)
        {
            lock (_sessions)
            {
                if (!_sessions.ContainsKey(session)) return false;

                _sessions[session] = weight;

                return true;
            }
        }
    }
}