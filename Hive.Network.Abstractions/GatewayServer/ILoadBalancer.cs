using System.Collections.Generic;
using Hive.Network.Abstractions.Session;

namespace Hive.Network.Abstractions.GatewayServer;

public interface ILoadBalancer
{
    ISession Get();
    IReadOnlyList<ISession> GetAll();

    void Add(ISession session);
    bool Remove(ISession session);

    void Clear();
}