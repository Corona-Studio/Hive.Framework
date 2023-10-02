using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Hive.Network.Shared
{
    public class GuaranteedDeliveryBroadcastBlock<T> : IPropagatorBlock<T, T>
    {
        private class Subscription
        {
            public readonly ITargetBlock<T> Target;
            public readonly bool PropagateCompletion;
            public readonly CancellationTokenSource CancellationSource;

            public Subscription(ITargetBlock<T> target,
                bool propagateCompletion,
                CancellationTokenSource cancellationSource)
            {
                Target = target;
                PropagateCompletion = propagateCompletion;
                CancellationSource = cancellationSource;
            }
        }

        private readonly object _locker = new();
        private readonly Func<T, T> _cloningFunction;
        private readonly CancellationToken _cancellationToken;
        private readonly ITargetBlock<T> _actionBlock;
        private readonly List<Subscription> _subscriptions = new();
        private CancellationTokenSource? _faultCts = new(); // Is nullified on completion

        public GuaranteedDeliveryBroadcastBlock(Func<T, T> cloningFunction,
            DataflowBlockOptions? dataFlowBlockOptions = null)
        {
            _cloningFunction = cloningFunction
                               ?? throw new ArgumentNullException(nameof(cloningFunction));
            dataFlowBlockOptions ??= new DataflowBlockOptions();
            _cancellationToken = dataFlowBlockOptions.CancellationToken;

            _actionBlock = new ActionBlock<T>(async item =>
            {
                Task sendAsyncToAll;
                lock (_locker)
                {
                    var allSendAsyncTasks = _subscriptions
                        .Select(sub => sub.Target.SendAsync(
                            _cloningFunction(item), sub.CancellationSource.Token));
                    sendAsyncToAll = Task.WhenAll(allSendAsyncTasks);
                }

                await sendAsyncToAll;
            }, new ExecutionDataflowBlockOptions
            {
                CancellationToken = dataFlowBlockOptions.CancellationToken,
                BoundedCapacity = dataFlowBlockOptions.BoundedCapacity,
                MaxMessagesPerTask = dataFlowBlockOptions.MaxMessagesPerTask,
                TaskScheduler = dataFlowBlockOptions.TaskScheduler
            });

            var afterCompletion = _actionBlock.Completion.ContinueWith(t =>
            {
                lock (_locker)
                {
                    // PropagateCompletion
                    foreach (var subscription in _subscriptions)
                        if (subscription.PropagateCompletion)
                        {
                            if (t.IsFaulted)
                                subscription.Target.Fault(t.Exception!);
                            else
                                subscription.Target.Complete();
                        }

                    // Cleanup
                    foreach (var subscription in _subscriptions) subscription.CancellationSource.Dispose();
                    _subscriptions.Clear();
                    _faultCts?.Dispose();
                    _faultCts = null; // Prevent future subscriptions to occur
                }
            }, TaskScheduler.Default);

            // Ensure that any exception in the continuation will be surfaced
            Completion = Task.WhenAll(_actionBlock.Completion, afterCompletion);
        }

        public Task Completion { get; }

        public void Complete()
        {
            _actionBlock.Complete();
        }

        void IDataflowBlock.Fault(Exception ex)
        {
            _actionBlock.Fault(ex);
            lock (_locker)
            {
                _faultCts?.Cancel();
            }
        }

        public IDisposable LinkTo(ITargetBlock<T> target,
            DataflowLinkOptions linkOptions)
        {
            if (linkOptions.MaxMessages != DataflowBlockOptions.Unbounded)
                throw new NotSupportedException();
            Subscription subscription;
            lock (_locker)
            {
                if (_faultCts == null) return new Unlinker(null); // Has completed
                var cancellationSource = CancellationTokenSource
                    .CreateLinkedTokenSource(_cancellationToken, _faultCts.Token);
                subscription = new Subscription(target,
                    linkOptions.PropagateCompletion, cancellationSource);
                _subscriptions.Add(subscription);
            }

            return new Unlinker(() =>
            {
                lock (_locker)
                {
                    // The subscription may have already been removed
                    if (_subscriptions.Remove(subscription))
                    {
                        subscription.CancellationSource.Cancel();
                        subscription.CancellationSource.Dispose();
                    }
                }
            });
        }

        private class Unlinker : IDisposable
        {
            private readonly Action? _action;

            public Unlinker(Action? disposeAction)
            {
                _action = disposeAction;
            }

            void IDisposable.Dispose()
            {
                _action?.Invoke();
            }
        }

        DataflowMessageStatus ITargetBlock<T>.OfferMessage(
            DataflowMessageHeader messageHeader, T messageValue,
            ISourceBlock<T>? source, bool consumeToAccept)
        {
            return _actionBlock.OfferMessage(messageHeader, messageValue, source,
                consumeToAccept);
        }

        T ISourceBlock<T>.ConsumeMessage(DataflowMessageHeader messageHeader,
            ITargetBlock<T> target, out bool messageConsumed)
        {
            throw new NotSupportedException();
        }

        bool ISourceBlock<T>.ReserveMessage(DataflowMessageHeader messageHeader,
            ITargetBlock<T> target)
        {
            throw new NotSupportedException();
        }

        void ISourceBlock<T>.ReleaseReservation(DataflowMessageHeader messageHeader,
            ITargetBlock<T> target)
        {
            throw new NotSupportedException();
        }
    }
}