using Hive.Network.Abstractions.Session;

namespace Hive.Both.General
{
    public delegate void DispatchHandler<in T>(IDispatcher dispatcher, ISession session, T message);
}