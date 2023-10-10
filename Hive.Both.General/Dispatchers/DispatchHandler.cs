using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Dispatchers
{
    public delegate void DispatchHandler<in T>(IDispatcher dispatcher, ISession session, T message);
}