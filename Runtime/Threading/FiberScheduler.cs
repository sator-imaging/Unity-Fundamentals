// (c) 2025 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

#if UNITY_5_3_OR_NEWER && DEBUG
#define __logging
#endif

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable SMA0040 // Missing Using Statement

namespace SatorImaging.UnityFundamentals
{
    /// <summary>
    /// Provides a mechanism to schedule and manage the execution of tasks with a specified degree of concurrency.
    /// </summary>
    /// <remarks>
    /// This scheduler is designed to run tasks in a controlled manner, allowing for suspension, resumption, and parallel execution of tasks.
    /// </remarks>
    public class FiberScheduler
    {
        [Conditional("__logging")]
        static void DEBUG(object msg, [CallerMemberName] string? callerMemberName = null)
        {
            // avoid compile error
#if __logging
            UnityEngine.Debug.Log($"[{callerMemberName}] {msg}");
#endif
        }


        /// <summary>
        /// The default scheduler.
        /// </summary>
        public static FiberScheduler Default { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

        /// <remarks>
        /// > [!TIP]
        /// > Did you deeply consider running priority task by using <see cref="Task.Run(Action)"/> or <c>await</c>?
        /// </remarks>
        public static FiberScheduler Priority { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

        static FiberScheduler()
        {
            int processorCount = Environment.ProcessorCount;

            Priority = new(processorCount)
            {
                State = "<Priority>",
            };

            Default = new(processorCount)
            {
                State = "<Default>",
            };

            // NOTE: DO NOT delete the DEBUG() call in event.
            //       --> on startup, event will resume the suspended default scheduler, and then the default
            //           scheduler will emit a redundant log.
            //           Without event log, it seems that 2 default schedulers run simultaneously.
            //       * It's not a good idea to use Task.Run to wait for initialization in the thread pool.
            Priority.OnWillConsume += () =>
            {
                DEBUG($"{nameof(Priority)} scheduler suspends {nameof(Default)} scheduler");
                Default.Suspend();
            };
            Priority.OnDidConsume += () =>
            {
                DEBUG($"{nameof(Priority)} scheduler resumes {nameof(Default)} scheduler");
                Default.Resume();
            };
        }


        /*  instance  ================================================================ */

        readonly ConcurrentQueue<(Payload payload, Func<Payload, ValueTask> factory)> taskQueue = new();

        /// <summary>
        /// Create a new instance of the <see cref="FiberScheduler"/> class.
        /// </summary>
        /// <param name="concurrency">The number of tasks to run in parallel.</param>
        public FiberScheduler(int concurrency)
        {
            if (concurrency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(concurrency), $"Argument must be greater than 0: {concurrency}");
            }

            this.b_concurrency = concurrency;

            Resume();
        }


        /// <summary>
        /// Gets or sets an object that contains data about the scheduler.
        /// </summary>
        public object? State { get; set; }


        /// <summary>
        /// Occurs before the scheduler begins consuming a task.
        /// </summary>
        public event Action? OnWillConsume;
        /// <summary>
        /// Occurs after all tasks in the queue have been consumed.
        /// </summary>
        public event Action? OnDidConsume;

        /// <summary>
        /// Occurs when an error is encountered during task execution.
        /// </summary>
        public event Action<FiberScheduler, Payload, Exception>? ErrorHandler;


        volatile int b_concurrency;

        /// <summary>
        /// Adjusts the concurrency level by the specified delta in a thread-safe manner.
        /// <para>
        /// > [!IMPORTANT]
        /// > Increment exactly same value you've previously decrement or vice versa to
        /// > restore original state in thread-safe manner.
        /// > (i.e., you should not calculate delta right before restoring your modification. it's not thread-safe)
        /// </para>
        /// </summary>
        /// <param name="delta">The amount to change the concurrency level by.</param>
        /// <returns>The new concurrency level.</returns>
        public int AdjustConcurrencyLevel(int delta)
        {
            return Interlocked.Add(ref b_concurrency, delta);
        }

        /// <summary>
        /// Gets a reference to the raw concurrency level field.
        /// <para>
        /// > [!IMPORTANT]
        /// > Direct modification is not thread-safe.
        /// > For thread-safe modifications, use atomic operations or appropriate synchronization mechanisms.
        /// </para>
        /// </summary>
        public ref int RawConcurrencyLevel => ref b_concurrency;


        /// <summary>
        /// Schedules a new task to be executed by the scheduler.
        /// </summary>
        /// <param name="payload">The payload to be processed by the task.</param>
        /// <param name="factory">A function that creates the task to be executed.</param>
        public void Submit(Payload payload, Func<Payload, ValueTask> factory)
        {
            taskQueue.Enqueue((payload, factory));

            if (interlock_runningThreadCount == 0)
            {
                ConsumeNextAvailableTask();
            }
        }

        /// <summary>
        /// Gets the number of tasks remaining in the queue.
        /// </summary>
        public int RemainingTaskCount => taskQueue.Count;

        /// <summary>
        /// Gets a value indicating whether the scheduler is currently running.
        /// </summary>
        public bool IsRunning => interlock_isRunning != 0;

        volatile int interlock_isRunning;
        volatile int interlock_runningThreadCount;
        volatile int interlock_isConsuming;

        /// <summary>
        /// Suspends the execution of the scheduler.
        /// </summary>
        /// <returns>The number of tasks remaining in the queue when the scheduler was suspended.</returns>
        public int Suspend()
        {
            Interlocked.Exchange(ref interlock_isRunning, 0);
            return taskQueue.Count;
        }

        /// <summary>
        /// Resumes the execution of a suspended scheduler.
        /// </summary>
        public void Resume()
        {
            if (Interlocked.Exchange(ref interlock_isRunning, 1) != 0)
            {
                return;
            }

            ConsumeNextAvailableTask();
        }

        /* =====  consuming thread  ===== */

        void ConsumeNextAvailableTask()
        {
            while (interlock_isRunning != 0 && interlock_runningThreadCount < b_concurrency)
            {
                if (Interlocked.Increment(ref interlock_runningThreadCount) > b_concurrency ||
                    !taskQueue.TryDequeue(out var job))
                {
                    Interlocked.Decrement(ref interlock_runningThreadCount);
                    return;
                }

                if (Interlocked.Exchange(ref interlock_isConsuming, 1) == 0)
                {
                    OnWillConsume?.Invoke();
                }

                ThreadPool.UnsafeQueueUserWorkItem(static async (obj) =>
                {
                    var (self, payload, factory) = ((FiberScheduler, Payload, Func<Payload, ValueTask>))obj;
                    try
                    {
                        await factory.Invoke(payload);
                    }
                    catch (Exception error)
                    {
                        DEBUG(error);

                        self.ErrorHandler?.Invoke(self, payload, error);
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref self.interlock_runningThreadCount) == 0)
                        {
                            if (Interlocked.Exchange(ref self.interlock_isConsuming, 0) != 0)
                            {
                                if (self.interlock_runningThreadCount == 0 && self.taskQueue.IsEmpty)
                                {
                                    self.OnDidConsume?.Invoke();

                                    DEBUG($"Scheduler '{self.State ?? "NO STATE AVAILABLE"}' has finished task execution");
                                }
                            }
                        }

                        self.ConsumeNextAvailableTask();  // always retry consuming new task
                    }
                },
                (this, job.payload, job.factory));
            }
        }


        /*  payload  ================================================================ */

        /// <summary>
        /// A payload for the task.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct Payload : IEquatable<Payload>
        {
            /// <summary>
            /// A value associated with the payload (e.g., timestamp).
            /// </summary>
            public readonly long Value;
            /// <summary>
            /// The optional state of the task.
            /// </summary>
            public readonly object? State;

            /// <summary>
            /// Create a new instance of the <see cref="Payload"/> struct.
            /// </summary>
            /// <param name="Value">A value associated with the payload (e.g., timestamp).</param>
            /// <param name="State">The optional state of the task.</param>
            public Payload(long Value, object? State)
            {
                this.Value = Value;
                this.State = State;
            }

            public override int GetHashCode() => HashCode.Combine(this.Value, this.State);
            public override bool Equals(object? obj) => obj is Payload other && Equals(other);
            public bool Equals(Payload other)
            {
                return other.Value == Value
                    && other.State == State
                    ;
            }
            public static bool operator ==(Payload left, Payload right) => left.Equals(right);
            public static bool operator !=(Payload left, Payload right) => !(left == right);
        }
    }
}
