using System;
using System.Threading;
using System.Threading.Tasks;
using Hive.Network.Abstractions.Session;

namespace Hive.Both.General.Dispatchers
{
    public interface IDispatcher
    {
        void Dispatch(ISession session, ReadOnlyMemory<byte> rawMessage);

        void Dispatch<T>(ISession session, T message) where T : class;
        void Dispatch(ISession session, Type type, object message);

        HandlerId AddHandler<T>(DispatchHandler<T> handler, TaskScheduler? scheduler = null);

        bool RemoveHandler<T>(DispatchHandler<T> handler);
        bool RemoveHandler(HandlerId id);

        Task<T> HandleOnce<T>(ISession session, CancellationToken cancellationToken = default);

        Task<TResp?> SendAndListenOnce<TReq, TResp>(ISession session, TReq message,
            CancellationToken cancellationToken = default);

        ValueTask<bool> SendAsync<T>(ISession session, T message);
    }
}