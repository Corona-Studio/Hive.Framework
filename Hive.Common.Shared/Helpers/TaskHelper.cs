using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hive.Common.Shared.Helpers
{
    /// <summary>
    ///     管理Task和CancellationTokenSource
    /// </summary>
    public static class TaskHelper
    {
        public const int OptimalMaxSpinWaitsPerSpinIteration = 12;

        private static readonly TaskFactory TaskFactory = new(TaskScheduler.Default);

        public static Task<Task> Fire(Func<Task> func)
        {
            return TaskFactory.StartNew(async () => await func());
        }

        public static void FireAndForget(Func<Task> func)
        {
            try
            {
                TaskFactory.StartNew(async () => await func());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static async Task WaitUtil(Func<bool> condition, int interval = 5)
        {
            while (!condition()) await Task.Delay(interval);
        }

        public static async Task WaitUtil(Func<bool> condition, CancellationToken ct, int interval = 5)
        {
            try
            {
                while (!ct.IsCancellationRequested && !condition()) await Task.Delay(interval, ct);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Task canceled");
            }
        }

        public static async Task WaitWhile(Func<bool> condition, CancellationToken ct, int interval = 5)
        {
            try
            {
                while (!ct.IsCancellationRequested && condition()) await Task.Delay(interval, ct);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Task canceled");
            }
        }

        /// <summary>
        ///     await一个task，可以抛出其中的异常
        /// </summary>
        /// <param name="task"></param>
        /// <param name="exceptionTypes"></param>
        public static async void CatchException(
            this Task task,
            params Type[] exceptionTypes)
        {
            try
            {
                await task;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Task canceled");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                if (exceptionTypes?.Any(a => a == e.GetType()) ?? false)
                    return;

                throw;
            }
        }
    }

    /// <summary>
    ///     直接从SpinWait里复制的，把Thread.yield之类的修改为Task.Yield
    ///     不知道效果怎么样
    /// </summary>
    public class SpinWaitAsync
    {
        // These constants determine the frequency of yields versus spinning. The
        // numbers may seem fairly arbitrary, but were derived with at least some
        // thought in the design document.  I fully expect they will need to change
        // over time as we gain more experience with performance.
        internal const int YieldThreshold = 10; // When to switch over to a true yield.
        private const int Sleep0EveryHowManyYields = 5; // After how many yields should we Sleep(0)?
        internal const int DefaultSleep1Threshold = 20; // After how many yields should we Sleep(1) frequently?

        // The number of times we've spun already.
        private int _count;

        /// <summary>
        ///     Gets the number of times <see cref="SpinOnce()" /> has been called on this instance.
        /// </summary>
        public int Count
        {
            get => _count;
            internal set
            {
                Debug.Assert(value >= 0);
                _count = value;
            }
        }

        /// <summary>
        ///     Gets whether the next call to <see cref="SpinOnce()" /> will yield the processor, triggering a
        ///     forced context switch.
        /// </summary>
        /// <value>
        ///     Whether the next call to <see cref="SpinOnce()" /> will yield the processor, triggering a
        ///     forced context switch.
        /// </value>
        /// <remarks>
        ///     On a single-CPU machine, <see cref="SpinOnce()" /> always yields the processor. On machines with
        ///     multiple CPUs, <see cref="SpinOnce()" /> may yield after an unspecified number of calls.
        /// </remarks>
        public bool NextSpinWillYield => _count >= YieldThreshold; //|| Environment.IsSingleProcessor;

        /// <summary>
        ///     Performs a single spin.
        /// </summary>
        /// <remarks>
        ///     This is typically called in a loop, and may change in behavior based on the number of times a
        ///     <see cref="SpinOnce()" /> has been called thus far on this instance.
        /// </remarks>
        public async ValueTask SpinOnce()
        {
            await SpinOnceCore(DefaultSleep1Threshold);
        }

        /// <summary>
        ///     Performs a single spin.
        /// </summary>
        /// <param name="sleep1Threshold">
        ///     A minimum spin count after which <code>Task.Delay(1)</code> may be used. A value of <code>-1</code> may be used to
        ///     disable the use of <code>Task.Delay(1)</code>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <paramref name="sleep1Threshold" /> is less than <code>-1</code>.
        /// </exception>
        /// <remarks>
        ///     This is typically called in a loop, and may change in behavior based on the number of times a
        ///     <see cref="SpinOnce()" /> has been called thus far on this instance.
        /// </remarks>
        public async Task SpinOnce(int sleep1Threshold)
        {
            sleep1Threshold = sleep1Threshold switch
            {
                < -1 => throw new ArgumentOutOfRangeException(nameof(sleep1Threshold), sleep1Threshold,
                    "sleep1Threshold < -1"),
                >= 0 and < YieldThreshold => YieldThreshold,
                _ => sleep1Threshold
            };

            await SpinOnceCore(sleep1Threshold);
        }

        private async ValueTask SpinOnceCore(int sleep1Threshold)
        {
            Debug.Assert(sleep1Threshold >= -1);
            Debug.Assert(sleep1Threshold is < 0 or >= YieldThreshold);

            // (_count - YieldThreshold) % 2 == 0: The purpose of this check is to interleave Thread.Yield/Sleep(0) with
            // Thread.SpinWait. Otherwise, the following issues occur:
            //   - When there are no threads to switch to, Yield and Sleep(0) become no-op and it turns the spin loop into a
            //     busy-spin that may quickly reach the max spin count and cause the thread to enter a wait state, or may
            //     just busy-spin for longer than desired before a Sleep(1). Completing the spin loop too early can cause
            //     excessive context switcing if a wait follows, and entering the Sleep(1) stage too early can cause
            //     excessive delays.
            //   - If there are multiple threads doing Yield and Sleep(0) (typically from the same spin loop due to
            //     contention), they may switch between one another, delaying work that can make progress.
            if (_count >= YieldThreshold &&
                ((_count >= sleep1Threshold && sleep1Threshold >= 0) || (_count - YieldThreshold) % 2 == 0) /*||
                    Environment.IsSingleProcessor*/)
            {
                //
                // We must yield.
                //
                // We prefer to call Thread.Yield first, triggering a SwitchToThread. This
                // unfortunately doesn't consider all runnable threads on all OS SKUs. In
                // some cases, it may only consult the runnable threads whose ideal processor
                // is the one currently executing code. Thus we occasionally issue a call to
                // Sleep(0), which considers all runnable threads at equal priority. Even this
                // is insufficient since we may be spin waiting for lower priority threads to
                // execute; we therefore must call Sleep(1) once in a while too, which considers
                // all runnable threads, regardless of ideal processor and priority, but may
                // remove the thread from the scheduler's queue for 10+ms, if the system is
                // configured to use the (default) coarse-grained system timer.
                //

                if (_count >= sleep1Threshold && sleep1Threshold >= 0)
                {
                    await Task.Delay(1);
                }
                else
                {
                    var yieldsSoFar = _count >= YieldThreshold ? (_count - YieldThreshold) / 2 : _count;
                    if (yieldsSoFar % Sleep0EveryHowManyYields == Sleep0EveryHowManyYields - 1)
                        await Task.Delay(0);
                    else
                        await Task.Yield();
                }
            }
            else
            {
                //
                // Otherwise, we will spin.
                //
                // We do this using the CLR's SpinWait API, which is just a busy loop that
                // issues YIELD/PAUSE instructions to ensure multi-threaded CPUs can react
                // intelligently to avoid starving. (These are NOOPs on other CPUs.) We
                // choose a number for the loop iteration count such that each successive
                // call spins for longer, to reduce cache contention.  We cap the total
                // number of spins we are willing to tolerate to reduce delay to the caller,
                // since we expect most callers will eventually block anyway.
                //
                // Also, cap the maximum spin count to a value such that many thousands of CPU cycles would not be wasted doing
                // the equivalent of YieldProcessor(), as at that point SwitchToThread/Sleep(0) are more likely to be able to
                // allow other useful work to run. Long YieldProcessor() loops can help to reduce contention, but Sleep(1) is
                // usually better for that.
                var n = TaskHelper.OptimalMaxSpinWaitsPerSpinIteration;
                if (_count <= 30 && 1 << _count < n) n = 1 << _count;

                Thread.SpinWait(n);
            }

            // Finally, increment our spin counter.
            _count = _count == int.MaxValue ? YieldThreshold : _count + 1;
        }

        /// <summary>
        ///     Resets the spin counter.
        /// </summary>
        /// <remarks>
        ///     This makes <see cref="SpinOnce()" /> and <see cref="NextSpinWillYield" /> behave as though no calls
        ///     to <see cref="SpinOnce()" /> had been issued on this instance. If a <see cref="SpinWait" /> instance
        ///     is reused many times, it may be useful to reset it to avoid yielding too soon.
        /// </remarks>
        public void Reset()
        {
            _count = 0;
        }

        #region Static Methods

        /// <summary>
        ///     Spins until the specified condition is satisfied.
        /// </summary>
        /// <param name="condition">A delegate to be executed over and over until it returns true.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="condition" /> argument is null.</exception>
        public static async Task SpinUntil(Func<bool> condition)
        {
#if DEBUG
            var result =
#endif
                await SpinUntil(condition, Timeout.Infinite);
#if DEBUG
            Debug.Assert(result);
#endif
        }

        /// <summary>
        ///     Spins until the specified condition is satisfied or until the specified timeout is expired.
        /// </summary>
        /// <param name="condition">A delegate to be executed over and over until it returns true.</param>
        /// <param name="timeout">
        ///     A <see cref="TimeSpan" /> that represents the number of milliseconds to wait,
        ///     or a TimeSpan that represents -1 milliseconds to wait indefinitely.
        /// </param>
        /// <returns>True if the condition is satisfied within the timeout; otherwise, false</returns>
        /// <exception cref="ArgumentNullException">The <paramref name="condition" /> argument is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        ///     <paramref name="timeout" /> is a negative number
        ///     other than -1 milliseconds, which represents an infinite time-out -or- timeout is greater than
        ///     <see cref="int.MaxValue" />.
        /// </exception>
        public static async Task<bool> SpinUntil(Func<bool> condition, TimeSpan timeout)
        {
            // Validate the timeout
            var totalMilliseconds = (long)timeout.TotalMilliseconds;
            if (totalMilliseconds < -1 || totalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(timeout), timeout, "SpinUntil timeout out of range.");

            // Call wait with the timeout milliseconds
            return await SpinUntil(condition, (int)totalMilliseconds);
        }

        /// <summary>
        ///     Spins until the specified condition is satisfied or until the specified timeout is expired.
        /// </summary>
        /// <param name="condition">A delegate to be executed over and over until it returns true.</param>
        /// <param name="millisecondsTimeout">
        ///     The number of milliseconds to wait, or
        ///     <see
        ///         cref="System.Threading.Timeout.Infinite" />
        ///     (-1) to wait indefinitely.
        /// </param>
        /// <returns>True if the condition is satisfied within the timeout; otherwise, false</returns>
        /// <exception cref="ArgumentNullException">The <paramref name="condition" /> argument is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        ///     <paramref name="millisecondsTimeout" /> is a
        ///     negative number other than -1, which represents an infinite time-out.
        /// </exception>
        public static async Task<bool> SpinUntil(Func<bool> condition, int millisecondsTimeout)
        {
            if (millisecondsTimeout < Timeout.Infinite)
                throw new ArgumentOutOfRangeException(
                    nameof(millisecondsTimeout), millisecondsTimeout, "SpinUntil millisecondsTimeout out of range");

            if (condition == null)
                throw new ArgumentNullException(nameof(condition), "SpinUntil condition can't be null.");

            uint startTime = 0;
            if (millisecondsTimeout != 0 && millisecondsTimeout != Timeout.Infinite)
                startTime = (uint)Environment.TickCount;

            var spinner = new SpinWaitAsync();
            while (!condition())
            {
                if (millisecondsTimeout == 0) return false;

                await spinner.SpinOnce();

                if (millisecondsTimeout == Timeout.Infinite || !spinner.NextSpinWillYield) continue;
                if (millisecondsTimeout <= (uint)Environment.TickCount - startTime)
                    return false;
            }

            return true;
        }

        #endregion
    }
}