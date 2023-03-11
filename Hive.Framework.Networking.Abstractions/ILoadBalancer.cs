using System.Collections.Generic;

namespace Hive.Framework.Networking.Abstractions;

public interface ILoadBalancer<TSession> where TSession : ISession<TSession>
{
    void AddSession(TSession sessionInfo);
    bool RemoveSession(TSession sessionInfo);
    TSession GetOne();
    IReadOnlyList<TSession> GetRawList();
}