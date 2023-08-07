using System;
using System.Collections.Generic;
using System.Linq;
using Hive.Framework.Networking.Abstractions;

namespace Hive.Framework.Networking.Shared.LoadBalancers;

public class BasicWeightedLoadBalancer<TSession> : ILoadBalancer<TSession> where TSession : ISession<TSession>
{
    private readonly Dictionary<TSession, ushort> _sessions = new ();
    private readonly Random _random = new();

    public void AddSession(TSession session)
    {
        _sessions.Add(session, 0);
    }

    public bool RemoveSession(TSession session)
    {
        var removed = _sessions.Remove(session, out var weight);

        return removed;
    }

    public bool UpdateWeight(TSession session, ushort weight)
    {
        if(!_sessions.ContainsKey(session)) return false;

        _sessions[session] = weight;

        return true;
    }

    public TSession GetOne()
    {
        var randomWeight = _random.Next(0, _sessions.Sum(p => p.Value));

        foreach (var (session, weight) in _sessions)
        {
            if (randomWeight < weight)
            {
                return session;
            }

            randomWeight -= weight;
        }

        return _sessions.First().Key;
    }

    public IReadOnlyList<TSession> GetRawList()
    {
        return _sessions.Select(p => p.Key).ToList().AsReadOnly();
    }
}