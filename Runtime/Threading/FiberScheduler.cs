// (c) 2025 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

#if UNITY_5_3_OR_NEWER && DEBUG
#define __logging
#endif

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
                AutoRetryOnError = true,
            };

            Default = new(processorCount)
            {
                State = "<Default>",
            };

            // NOTE: DO NOT delete the DEBUG() call in event.
            //       --> on startup, event will resumes slept default scheduler and then default
            //           scheduler will emit redundant log.
            //           without event log, it seems that 2 default scheduler runs simultaneously.
            //       * it's not good idea to use Task.Run to wait initialization in thread pool.
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

        readonly ConcurrentQueue<(Payload state, Func<Payload, Task<Instruction>> factory)> taskQueue = new();

        /// <summary>
        /// Create a new instance of the <see cref="FiberScheduler"/> class.
        /// </summary>
        /// <param name="concurrency">The number of tasks to run in parallel.</param>
        public FiberScheduler(int concurrency)
        {
            this.Concurrency = concurrency;

            Resume();
        }


        /// <summary>
        /// Gets or sets the maximum number of tasks to run in parallel.
        /// </summary>
        public int Concurrency { get; set; }

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
        /// Gets or sets a value indicating whether to automatically retry a task when an exception is thrown.
        /// </summary>
        public bool AutoRetryOnError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }


        /// <summary>
        /// Schedules a new task to be executed by the scheduler.
        /// </summary>
        /// <param name="state">The payload to be processed by the task.</param>
        /// <param name="factory">A function that creates the task to be executed.</param>
        /// <remarks>
        ...
        /// </remarks>
        public void Schedule(Payload state, Func<Payload, Task<Instruction>> factory)
        {
            taskQueue.Enqueue((state, factory));
            ConsumeNextAvailableTask();
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
        volatile int interlock_runningTaskCount;
        volatile int interlock_isConsuming;

        /// <summary>
        /// Suspends the execution of the scheduler.
        /// </summary>
        /// <returns>The number of tasks remaining in the queue when the scheduler was suspended.</returns>
        public int Suspend()
        {
            Interlocked.Exchange(ref interlock_isRunning, 0);
            return RemainingTaskCount;
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
            while (interlock_isRunning != 0 && interlock_runningTaskCount < Concurrency)
            {
                if (Interlocked.Increment(ref interlock_runningTaskCount) > Concurrency)
                {
                    Interlocked.Decrement(ref interlock_runningTaskCount);
                    return;
                }

                if (!taskQueue.TryDequeue(out var job))
                {
                    Interlocked.Decrement(ref interlock_runningTaskCount);
                    return;
                }

                if (Interlocked.Exchange(ref interlock_isConsuming, 1) == 0)
                {
                    OnWillConsume?.Invoke();
                }

                ThreadPool.UnsafeQueueUserWorkItem(static async (obj) =>
                {
                    var (self, state, factory) = ((FiberScheduler, Payload, Func<Payload, Task<Instruction>>))obj;
                    try
                    {
                        var instruction = await factory(state);
                        switch (instruction)
                        {
                            case Instruction.Suspend:
                                self.Suspend();
                                return;
                            case Instruction.None:
                            default:
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        DEBUG(e);
                        if (self.AutoRetryOnError)
                        {
                            self.Schedule(state, factory);
                        }
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref self.interlock_runningTaskCount) == 0)
                        {
                            if (Interlocked.Exchange(ref self.interlock_isConsuming, 0) != 0)
                            {
                                if (self.interlock_runningTaskCount == 0 && self.RemainingTaskCount == 0)
                                    self.OnDidConsume?.Invoke();
                            }
                        }
                        self.ConsumeNextAvailableTask();
                    }
                }, (this, job.state, job.factory));
            }
        }


        /*  impl  ================================================================ */

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


        /// <summary>
        /// An instruction for the scheduler.
        /// </summary>
        public enum Instruction
        {
            /// <summary>Do nothing.</summary>
            None,

            /// <summary>Suspend scheduler immediately.</summary>
            Suspend,
        }


    }
}
