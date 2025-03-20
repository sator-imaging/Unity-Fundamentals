// (c) 2024-2025 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    // TODO: currently, submitted concurrent tasks are 'blocking' worker thread to achieve limit concurrent jobs ran by ConcurrentExclusiveSchedulerPair.
    //       ie. when concurrent level is set to 8, 8 threads are used (blocked) until job is completed.
    //       * actually, 'blocking' job uses 1 thread, and 1 another is used by actual job which is running in async/await context.
    //         total 2 or more threads are required to run concurrent job.
    //       to make concurrent operation more efficient, blocking op must be removed to free up worker threads to other.
    //       * to do so, create 1 sentinel thread to watch concurrently running job count.
    //         and when some job is finished, sentinel will dispatch another job up to specified concurrent level.
    public static class Run
    {
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void Editor_InitializeOnLoadMethod()
        {
            InitializeMainThreadContext();
        }
#endif

        /// <summary>NOTE: must be called on main thread.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void InitializeMainThreadContext()
        {
            _mainThread = SynchronizationContext.Current;
        }


        /// <summary>
        /// Invoked when error occurred. Return <see langword="true"/> if exception handled in your handler.
        /// (ie. stop flowing to subsequent call stack)
        /// <code>
        /// Args: (exception)
        /// </code>
        /// </summary>
        static Func<Exception, bool> _exceptionHandler = static (exc) =>
        {
            UnityEngine.Debug.LogException(exc);
            return false;
        };

        /// <inheritdoc cref="_exceptionHandler"/>
        public static void SetExceptionHandler(Func<Exception, bool> handler)
        {
            _exceptionHandler = handler;
        }


        /*  main thread  ================================================================ */

        static SynchronizationContext? _mainThread;

        [DoesNotReturn]
        static void ThrowMainThreadNotFound() => throw new NullReferenceException("main thread not found");


        /// <summary>
        /// NOTE: this method is blocking current thread until submitted main thread job is finished.
        /// </summary>
        /// <seealso cref="https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Scripting/UnitySynchronizationContext.cs#L37"/>
        public static void OnMainThread<T>(T state, Action<T> act)
        {
            if (_mainThread == null)
                ThrowMainThreadNotFound();

            (((_mainThread!))).Send(static (obj) =>
            {
                var (act, state) = ((Action<T>, T))obj;
                act.Invoke(state);
            },
            (act, state));
        }


        const string TASK_NOT_SUPPORTED = "Use `" + nameof(WaitForValueTaskCompletion) + "` to achieve blocking operation.";

        [Obsolete(TASK_NOT_SUPPORTED, true)]
        public static void OnMainThread<T>(T state, Func<T, CancellationToken, Task> task) => throw new NotSupportedException(TASK_NOT_SUPPORTED);

        [Obsolete(TASK_NOT_SUPPORTED, true)]
        public static void OnMainThread<T>(T state, Func<T, CancellationToken, ValueTask> task) => throw new NotSupportedException(TASK_NOT_SUPPORTED);


        /// <summary>
        /// NOTE: this method always <b>Post</b> job to main thread even though it is invoked on main thread.
        /// (ie. this method is immediately finished but submitted job is NOT finished immediately)
        /// </summary>
        /// <seealso cref="https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Scripting/UnitySynchronizationContext.cs#L60"/>
        public static void OnMainThreadAndForget<T>(T state, Action<T> act)
        {
            if (_mainThread == null)
                ThrowMainThreadNotFound();

            (((_mainThread!))).Post(static (obj) =>
            {
                var (act, state) = ((Action<T>, T))obj;
                act.Invoke(state);
            },
            (act, state));
        }


        /*  helper  ================================================================ */

        /// <param name="spareDelay">
        /// Cancellation delay is used to prevent mistake like --> <c>await Task.Delay(ms, GetTimerToken(ms));  // delay never met</c>
        /// </param>
        public static CancellationToken GetTimerToken(int milliseconds, int spareDelay = 256)
        {
            if (milliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(milliseconds));

            if (spareDelay < 0)
                spareDelay = 0;

            var cts = new CancellationTokenSource(milliseconds + spareDelay);
            var token = cts.Token;

            // register self disposing action...!!
            ThreadPool.QueueUserWorkItem(static (args) =>
            {
                args.token.Register(static (cts) => ((CancellationTokenSource)cts).Dispose(), args.cts, false);
            },
            (cts, token), false);

            return token;
        }


        /*  shutdown  ================================================================ */

        #region  GetElapsedTime(long) & GetElapsedTime(long, long)
        // https://github.com/dotnet/runtime/blob/v9.0.2/src/libraries/Microsoft.Extensions.Http/src/ValueStopwatch.cs#L11
        readonly static double s_timestampToTicks = System.TimeSpan.TicksPerSecond / (double)System.Diagnostics.Stopwatch.Frequency;
        /// <param name="startingTimestamp">Use <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static System.TimeSpan GetElapsedTime(long startingTimestamp)
        {
            long endingTimestamp = Stopwatch.GetTimestamp();
            return new System.TimeSpan((long)((endingTimestamp - startingTimestamp) * s_timestampToTicks));
        }
        /// <param name="startingTimestamp">Use <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>.</param>
        /// <param name="endingTimestamp">Use <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static System.TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp)
        {
            return new System.TimeSpan((long)((endingTimestamp - startingTimestamp) * s_timestampToTicks));
        }
        #endregion


        /// <summary>
        /// Wait for task completion in sync. Designed for blocking application quit before completion.
        /// </summary>
        public static void Shutdown(int maxWaitMilliseconds = -1)
        {
            using var wait = new ManualResetEventSlim(false);
            bool isSubmitted = ThreadPool.QueueUserWorkItem(static (wait) =>
            {
                _scheduler.Completion.ConfigureAwait(false).GetAwaiter().OnCompleted(() => wait.Set());
            },
            wait, false);

            _scheduler.Complete();

            if (maxWaitMilliseconds < 0)
            {
                if (!isSubmitted)
                    return;

                maxWaitMilliseconds = int.MaxValue;  // for simplicity
            }

            var startTimestamp = Stopwatch.GetTimestamp();

            var spin = new SpinWait();
            while (!wait.IsSet)
            {
                spin.SpinOnce();

                if (GetElapsedTime(startTimestamp).TotalMilliseconds > maxWaitMilliseconds)
                {
                    break;
                }
            }
        }


        /*  worker thread  ================================================================ */

        const TaskCreationOptions _taskCreationOptions = TaskCreationOptions.RunContinuationsAsynchronously;
        static ConcurrentExclusiveSchedulerPair _scheduler = CreateNewScheduler();

        /// <param name="numThreads">Less than or equal to <c>0</c> to use default value</param>
        public static ConcurrentExclusiveSchedulerPair CreateNewScheduler(int numThreads = 0)
        {
            if (numThreads <= 0)
                numThreads = Environment.ProcessorCount / 2;

            return new(TaskScheduler.Default, numThreads, numThreads);  // setting all args makes it stable (my feeling)
        }

        public static int ConcurrentThreadCount => _scheduler.ConcurrentScheduler.MaximumConcurrencyLevel;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetConcurrentThreadCount(int numThreads) => SetConcurrentThreadCount(ref _scheduler, numThreads);

        /// <param name="adjustMaxThreadCount">
        /// Resulting max thread count for <see cref="ThreadPool"/> will be equal to or greater than <c>numThreads * adjustMaxThreadCount</c>.
        /// </param>
        public static void SetConcurrentThreadCount(ref ConcurrentExclusiveSchedulerPair scheduler,
                                                    int numThreads,
                                                    int adjustMaxThreadCount = 3,
                                                    TaskScheduler? baseScheduler = null)
        {
            if (numThreads == scheduler.ConcurrentScheduler.MaximumConcurrencyLevel)
                return;

            baseScheduler ??= TaskScheduler.Default;
            var newScheduler = new ConcurrentExclusiveSchedulerPair(baseScheduler, numThreads, numThreads);  // setting all args makes it stable (my feeling)
            var previous = scheduler;
            Volatile.Write(ref scheduler, newScheduler);

            previous.Complete();

            ThreadPool.GetMinThreads(out _, out var minIOPort);
            ThreadPool.SetMinThreads(numThreads, minIOPort);

            int requestMaxThreads = numThreads * adjustMaxThreadCount;

            ThreadPool.GetMaxThreads(out var maxThreads, out var maxIOPort);
            if (maxThreads < requestMaxThreads)
            {
                ThreadPool.SetMaxThreads(requestMaxThreads, maxIOPort);
            }
        }


        /* =====  runnable  ===== */

        public interface IRunnable : IDisposable
        {
            public const bool ContinueOnCapturedContext = false;  // false for performance. it can be set to true w/o deadlock
            public const int SpinCount = 0;

            void Run();
        }


        public sealed class SyncJob : Poolable<SyncJob>, IRunnable
        {
            Action act = (((null!)));

            public static SyncJob GetInstance(Action act)
            {
                var result = GetInstance();

                result.act = act;

                return result;
            }

            protected override void OnWillReturnToPool()
            {
                this.act = (((null!)));
            }

            public void Run()
            {
                ThrowIfAlreadyReturnedToPool("job has already ran");

                try
                {
                    act.Invoke();
                }
                catch (Exception exc)
                {
                    if (!_exceptionHandler(exc))
                        throw;
                }
            }
        }

        public sealed class SyncJob<TState> : Poolable<SyncJob<TState>>, IRunnable
        {
            Action<TState> act = (((null!)));
            TState state = (((default!)));

            public static SyncJob<TState> GetInstance(TState state, Action<TState> act)
            {
                var result = GetInstance();

                result.act = act;
                result.state = state;

                return result;
            }

            protected override void OnWillReturnToPool()
            {
                this.act = (((null!)));
                this.state = (((default!)));
            }

            public void Run()
            {
                ThrowIfAlreadyReturnedToPool("job has already ran");

                try
                {
                    act.Invoke(state);
                }
                catch (Exception exc)
                {
                    if (!_exceptionHandler(exc))
                        throw;
                }
            }
        }


        public static class SyncTask
        {
            readonly static Func<Func<CancellationToken, ValueTask>, CancellationToken, ValueTask> RunValueTask = static (func, ct) => func.Invoke(ct);

            public static SyncTask<Func<CancellationToken, ValueTask>> GetInstance(Func<CancellationToken, ValueTask> func, CancellationToken cancellationToken)
            {
                return SyncTask<Func<CancellationToken, ValueTask>>.GetInstance(func, RunValueTask, cancellationToken);
            }
        }


        public sealed class SyncTask<TState> : Poolable<SyncTask<TState>>, IRunnable
        {
            Func<TState, CancellationToken, ValueTask> func = (((null!)));
            TState state = (((default!)));
            CancellationToken cancellationToken;
            ExceptionDispatchInfo? error;
            readonly ManualResetEventSlim signal = new(false, IRunnable.SpinCount);

            public static SyncTask<TState> GetInstance(TState state, Func<TState, CancellationToken, ValueTask> func, CancellationToken cancellationToken)
            {
                var result = GetInstance();

                result.func = func;
                result.state = state;
                result.cancellationToken = cancellationToken;
                result.error = null;
                result.signal.Reset();

                return result;
            }

            protected override void OnWillReturnToPool()
            {
                this.func = (((null!)));
                this.state = (((default!)));
                this.cancellationToken = default;
                this.error = null;
                this.signal.Reset();
            }

            public void Run()
            {
                ThrowIfAlreadyReturnedToPool("job has already ran");

                try
                {
                    if (ThreadPool.UnsafeQueueUserWorkItem(RunTaskAsync, this))
                    {
                        signal.Wait(this.cancellationToken);
                        error?.Throw();
                    }
                }
                catch (Exception exc)
                {
                    if (!_exceptionHandler(exc))
                        throw;
                }
            }


            // NOTE: submit 'job dispatcher' job to concurrent task scheduler to achieve limiting count of concurrent jobs.
            //       note that submitted 'Func<ValueTask>' will be finished immediately so don't queue that. submit 'job scheduler' instead.
            //       example:
            //       - syncJob
            //       - jobDispatcherJob  <-- correct. blocking concurrent task scheduler (run in worker thread)
            //       - Func<ValueTask>   <-- incorrect!! don't block scheduler!!
            //       - syncJob           <-- unexpectedly this will run simultaneously with Func<ValueTask> cuz task scheduler is not blocked!!

            readonly static WaitCallback RunTaskAsync = async static (obj) =>
            {
                if (obj is not SyncTask<TState> runnable)
                {
                    const string msg = "Error occurred in thread pool. Restarting Unity is recommended.";
                    UnityEngine.Debug.LogError(msg);
                    throw new Exception(msg);
                }

                try
                {
                    // this path may not be executed immediately if many of jobs are in queue. check token first
                    runnable.cancellationToken.ThrowIfCancellationRequested();

                    await runnable.func.Invoke(runnable.state, runnable.cancellationToken).ConfigureAwait(IRunnable.ContinueOnCapturedContext);

                    runnable.cancellationToken.ThrowIfCancellationRequested();  // once again!!
                }
                catch (Exception exc)
                {
                    if (exc is AggregateException aggregate)
                    {
                        aggregate = aggregate.Flatten();

                        var inners = aggregate.InnerExceptions;
                        if (inners.Count == 1)
                        {
                            exc = inners[0];
                        }
                    }

                    runnable.error = ExceptionDispatchInfo.Capture(exc);
                }
                finally
                {
                    runnable.signal.Set();
                }
            };
        }


        /*  value task awaiter  ================================================================ */

        public static void WaitForValueTaskCompletion<TState>(TState state, Func<TState, CancellationToken, ValueTask> func, CancellationToken cancellationToken = default)
        {
            using var job = SyncTask<TState>.GetInstance(state, func, cancellationToken);
            job.Run();
        }

        public static void WaitForValueTaskCompletion(Func<CancellationToken, ValueTask> func, CancellationToken cancellationToken = default)
        {
            using var job = SyncTask.GetInstance(func, cancellationToken);
            job.Run();
        }


        readonly static Func<Func<CancellationToken, Task>, CancellationToken, ValueTask> WrapAndRunTaskAsync = async static (func, ct)
            => await func.Invoke(ct).ConfigureAwait(IRunnable.ContinueOnCapturedContext);

        public static void WaitForTaskCompletion(Func<CancellationToken, Task> func, CancellationToken cancellationToken)
        {
            using var job = SyncTask<Func<CancellationToken, Task>>.GetInstance(func, WrapAndRunTaskAsync, cancellationToken);
            job.Run();
        }


        /*  submission  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InThreadPool(IRunnable job, CancellationToken cancellationToken = default)
            => InThreadPool(job, _scheduler.ConcurrentScheduler, cancellationToken);

        public static void InThreadPool(IRunnable job, TaskScheduler scheduler, CancellationToken cancellationToken = default)
        {
            Task.Factory.StartNew(static obj =>
            {
                using var runnable = (IRunnable)obj;
                runnable.Run();
            },
            job,
            cancellationToken, _taskCreationOptions, scheduler);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InThreadPool(Action act) => InThreadPool(SyncJob.GetInstance(act), _scheduler.ConcurrentScheduler);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InThreadPool<T>(T state, Action<T> act) => InThreadPool(SyncJob<T>.GetInstance(state, act), _scheduler.ConcurrentScheduler);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InThreadPool(Func<CancellationToken, ValueTask> func, CancellationToken cancellationToken = default)
            => InThreadPool(SyncTask.GetInstance(func, cancellationToken), _scheduler.ConcurrentScheduler, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InThreadPool<T>(T state, Func<T, CancellationToken, ValueTask> func, CancellationToken cancellationToken = default)
            => InThreadPool(SyncTask<T>.GetInstance(state, func, cancellationToken), _scheduler.ConcurrentScheduler, cancellationToken);

    }
}




#region ////////  TEMPLATE: Debug menu for Unity Editor  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.DEBUG.The_Run_Utility  // must be unique. don't reuse existing namespace
{
    static class UNITY_EDITOR_DEBUG  // don't change
    {
        const string MENU_ROOT = nameof(DEBUG) + "/" + nameof(The_Run_Utility) + "/";


        #region ////////  TEMPLATE: Debug Methods  ////////
        /*  TEMPLATE: Debug Methods  ================================================================ */

        static int act_count;

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Basic_Tests))]
        static async void Basic_Tests()
        {
            const int PAD = 24;
            const int WAIT = 1000;
            const int COUNT = 8;

            using var _ = Defer.New(Run.ConcurrentThreadCount, static (restore) => Run.SetConcurrentThreadCount(restore));
            Run.SetConcurrentThreadCount(COUNT);

            act_count = 0;

            var act = new Action(() =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                Task.Delay(WAIT / 3).ConfigureAwait(false).GetAwaiter().GetResult();
                Task.Delay(WAIT / 3).ConfigureAwait(true).GetAwaiter().GetResult();
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Action",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var actT = new Action<object?>((_) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                Task.Delay(WAIT / 3).ConfigureAwait(false).GetAwaiter().GetResult();
                Task.Delay(WAIT / 3).ConfigureAwait(true).GetAwaiter().GetResult();
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Action<T>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var task = new Func<CancellationToken, ValueTask>(async (ct) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(false);
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(true);
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Func<ValueTask>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var taskT = new Func<object?, CancellationToken, ValueTask>(async (_, ct) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(false);
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(true);
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Func<T, ValueTask>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            for (int i = 0; i < COUNT; i++)
            {
                Run.InThreadPool(act);
                Run.InThreadPool(null, actT);
                Run.InThreadPool(task);
                Run.InThreadPool(null, taskT);
            }

            Run.InThreadPool(() => throw new Exception("[Expected] error from " + nameof(Run.InThreadPool)));
            Run.InThreadPool(ct => throw new Exception("[Expected] error from " + nameof(Run.InThreadPool)), CancellationToken.None);


            UnityEngine.Debug.Log(nameof(Basic_Tests) + ": # of threads: " + COUNT);
            await Task.Delay(WAIT * 4 + 1000);

            UnityEngine.Debug.Log("main thread blocking test takes a while. <b>8 logs</b> must be shown");
            await Task.Delay(100);

            // sync blocking??
            Run.SyncJob.GetInstance(act).Run();
            Run.SyncJob<object?>.GetInstance(null, actT).Run();
            Run.SyncTask.GetInstance(task, default).Run();
            Run.SyncTask<object?>.GetInstance(null, taskT, default).Run();

            // submit main thread job from worker thread
            ThreadPool.QueueUserWorkItem((_) =>
            {
                Run.OnMainThread((object?)null, (_) =>
                {
                    Run.SyncJob.GetInstance(act).Run();
                    Run.SyncJob<object?>.GetInstance(null, actT).Run();
                    Run.SyncTask.GetInstance(task, default).Run();
                    Run.SyncTask<object?>.GetInstance(null, taskT, default).Run();
                });
            },
            (object?)null, false);

            ThreadPool.QueueUserWorkItem((_) =>
            {
                Run.OnMainThread((object?)null, (_) => throw new Exception("[Expected] error from " + nameof(Run.OnMainThread)));
            });
            Run.OnMainThread((object?)null, (_) => throw new Exception("[Expected] error from " + nameof(Run.OnMainThread)));
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Few_Jobs))]
        static void Few_Jobs()
        {
            const int count = 3;
            RunTasks(count, count * 3);
        }

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Many_Jobs))]
        static void Many_Jobs()
        {
            int count = Environment.ProcessorCount;
            RunTasks(count, count * 3);
        }

        static void RunTasks(int count, int numThreads)
        {
            const int PAD = 24;
            const int WAIT = 1000;

            using var _ = Defer.New(Run.ConcurrentThreadCount, static (restore) => Run.SetConcurrentThreadCount(restore));
            Run.SetConcurrentThreadCount(numThreads);

            var notice = numThreads > Environment.ProcessorCount * 2 ? "  (takes so long time on first launch)" : string.Empty;
            UnityEngine.Debug.Log($"concurrent: {numThreads} / jobs: {count * 4}{notice}");

            act_count = 0;

            var act = new Action(() =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                Task.Delay(WAIT / 3).ConfigureAwait(false).GetAwaiter().GetResult();
                Task.Delay(WAIT / 3).ConfigureAwait(true).GetAwaiter().GetResult();
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Action",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var actT = new Action<object?>((_) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                Task.Delay(WAIT / 3).ConfigureAwait(false).GetAwaiter().GetResult();
                Task.Delay(WAIT / 3).ConfigureAwait(true).GetAwaiter().GetResult();
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Action<T>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var task = new Func<CancellationToken, ValueTask>(async (ct) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(false);
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(true);
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Func<ValueTask>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var taskT = new Func<object?, CancellationToken, ValueTask>(async (_, ct) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(false);
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(true);
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Func<T, ValueTask>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            for (int i = 0; i < count; i++)
            {
                Run.InThreadPool(act);
                Run.InThreadPool(null, actT);
                Run.InThreadPool(task);
                Run.InThreadPool(null, taskT);
            }

            Run.InThreadPool(() => throw new Exception("[Expected] error from " + nameof(Run.InThreadPool)));
            Run.InThreadPool(ct => throw new Exception("[Expected] error from " + nameof(Run.InThreadPool)), CancellationToken.None);
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Many_Jobs_by_Task_Run_ConfigureAwait_TRUE))]
        static void Many_Jobs_by_Task_Run_ConfigureAwait_TRUE()
        {
            RunTasksByTaskRunWithAsyncAwait(true);
        }

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Many_Jobs_by_Task_Run_ConfigureAwait_FALSE))]
        static void Many_Jobs_by_Task_Run_ConfigureAwait_FALSE()
        {
            RunTasksByTaskRunWithAsyncAwait(false);
        }

        static void RunTasksByTaskRunWithAsyncAwait(bool continueOnCapturedContext)
        {
            int count = Environment.ProcessorCount;

            const int PAD = 24;
            const int WAIT = 1000;

            UnityEngine.Debug.Log($"concurrent: unlimited / jobs: {count * 4} / continueOnCapturedContext: {continueOnCapturedContext}  (takes so long time on first launch)");

            act_count = 0;

            var act = new Action(() =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                Task.Delay(WAIT / 3).ConfigureAwait(false).GetAwaiter().GetResult();
                Task.Delay(WAIT / 3).ConfigureAwait(true).GetAwaiter().GetResult();
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Action",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var actT = new Action<object?>((_) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                Task.Delay(WAIT / 3).ConfigureAwait(false).GetAwaiter().GetResult();
                Task.Delay(WAIT / 3).ConfigureAwait(true).GetAwaiter().GetResult();
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Action<T>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var task = new Func<CancellationToken, ValueTask>(async (ct) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(false);
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(true);
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Func<ValueTask>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var taskT = new Func<object?, CancellationToken, ValueTask>(async (_, ct) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(false);
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(true);
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Func<T, ValueTask>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            for (int i = 0; i < count; i++)
            {
                Task.Run(() => act.Invoke());
                Task.Run(() => actT.Invoke(null));
                Task.Run(async () => await task.Invoke(default).ConfigureAwait(continueOnCapturedContext));
                Task.Run(async () => await taskT.Invoke(null, default).ConfigureAwait(continueOnCapturedContext));
            }

            Run.InThreadPool(() => throw new Exception("[Expected] error from " + nameof(Run.InThreadPool)));
            Run.InThreadPool(ct => throw new Exception("[Expected] error from " + nameof(Run.InThreadPool)), CancellationToken.None);
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Many_Jobs_by_Task_Run_No_Await))]
        static void Many_Jobs_by_Task_Run_No_Await()
        {
            int count = Environment.ProcessorCount;

            const int PAD = 24;
            const int WAIT = 1000;

            UnityEngine.Debug.Log($"concurrent: unlimited / jobs: {count * 4} / no await  (takes so long time on first launch)");

            act_count = 0;

            var act = new Action(() =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                Task.Delay(WAIT / 3).ConfigureAwait(false).GetAwaiter().GetResult();
                Task.Delay(WAIT / 3).ConfigureAwait(true).GetAwaiter().GetResult();
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Action",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var actT = new Action<object?>((_) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                Task.Delay(WAIT / 3).ConfigureAwait(false).GetAwaiter().GetResult();
                Task.Delay(WAIT / 3).ConfigureAwait(true).GetAwaiter().GetResult();
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Action<T>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var task = new Func<CancellationToken, ValueTask>(async (ct) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(false);
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(true);
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Func<ValueTask>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            var taskT = new Func<object?, CancellationToken, ValueTask>(async (_, ct) =>
            {
                var start = Stopwatch.GetTimestamp();
                var num = Interlocked.Increment(ref act_count);

                // deadlock must not happen
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(false);
                await Task.Delay(WAIT / 3, ct).ConfigureAwait(true);
                Thread.Sleep(WAIT / 3);

                var elapsed = Run.GetElapsedTime(start).TotalMilliseconds;
                UnityEngine.Debug.Log($"{"Func<T, ValueTask>",PAD}: \t{num} \tapprox. {Math.Round(elapsed * 0.01) * 0.1} secs  ({elapsed:#,0})");
            });

            for (int i = 0; i < count; i++)
            {
                Task.Run(() => act.Invoke());
                Task.Run(() => actT.Invoke(null));
                Task.Run(() => task.Invoke(default));
                Task.Run(() => taskT.Invoke(null, default));
            }

            Run.InThreadPool(() => throw new Exception("[Expected] error from " + nameof(Run.InThreadPool)));
            Run.InThreadPool(ct => throw new Exception("[Expected] error from " + nameof(Run.InThreadPool)), CancellationToken.None);
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Run_and_SetNumThreads_then_Run_Again_Immediately))]
        static void Run_and_SetNumThreads_then_Run_Again_Immediately()
        {
            const int count = 3;
            RunTasks(count, count * 3);

            Run.SetConcurrentThreadCount(Run.ConcurrentThreadCount + 1);

            RunTasks(count, count * 3);
        }


        /*  TEMPLATE: End of Debug  ================================================================ */
        #endregion    //  TEMPLATE: End of Debug


        /* TEMPLATE: copy & paste and replace argument for 'nameof()'

        [UnityEditor.MenuItem(MENU_ROOT + nameof(__Underscore_Separated_Method_Name__), priority = 0)]
        static void Basic_Tests()
        {
        }

        */


        // TEMPLATE: open script file
        [UnityEditor.MenuItem(MENU_ROOT + "Edit Debug Script...", priority = int.MaxValue - 310)]
        static void UnityEditorTests_EditDebugScript() => __EditDebugScript();

        static void __EditDebugScript(
            [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
    }
}
#endif
#endregion
