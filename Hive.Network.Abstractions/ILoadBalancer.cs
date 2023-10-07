using System.Collections.Generic;
using Hive.Network.Abstractions.Session;

namespace Hive.Network.Abstractions;

public interface ILoadBalancer<TSession> : IEnumerable<TSession> where TSession : ISession
{
    int Available { get; }

    /// <summary>
    ///     向负载均衡器注册一个可用的服务器会话
    ///     <para>默认情况下，刚添加的服务器会话都将被标记为可用</para>
    /// </summary>
    /// <param name="sessionInfo"></param>
    void AddSession(TSession sessionInfo);

    /// <summary>
    ///     从负载均衡器移除一个服务器会话
    /// </summary>
    /// <param name="sessionInfo"></param>
    /// <returns></returns>
    bool RemoveSession(TSession sessionInfo);

    /// <summary>
    ///     更新某个服务器会话的可用性
    /// </summary>
    /// <param name="sessionInfo"></param>
    /// <param name="available"></param>
    /// <returns></returns>
    bool UpdateSessionAvailability(TSession sessionInfo, bool available);

    TSession? GetOne();
    IReadOnlyList<TSession> GetRawList();
}