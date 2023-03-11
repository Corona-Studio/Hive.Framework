using System.Collections.Generic;
using Hive.Framework.Networking.Abstractions;

namespace Hive.Framework.Networking.Shared.LoadBalancers;

public class BasicLoadBalancer<TSession> : ILoadBalancer<TSession> where TSession : ISession<TSession>
{
    private readonly List<TSession> _sessions = new ();
    private int _currentIndex;

    public void AddSession(TSession sessionInfo)
    {
        if(_sessions.Contains(sessionInfo)) return;

        _sessions.Add(sessionInfo);
    }

    public bool RemoveSession(TSession sessionInfo)
    {
        return _sessions.Remove(sessionInfo);
    }

    public TSession GetOne()
    {
        var result = _sessions[_currentIndex];

        _currentIndex = (_currentIndex + 1) % _sessions.Count;

        return result;
    }

    public IReadOnlyList<TSession> GetRawList()
    {
        return _sessions.AsReadOnly();
    }
}