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


        public static FiberScheduler Default { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

        /// <summary>
        /// Did you deeply consider running priority task by using <see cref="Task.Run(Action)"/> or  <c>await</c>?
        /// </summary>
        public static FiberScheduler Priority { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

        static FiberScheduler()
        {
            int processorCount = Environment.ProcessorCount;

            Default = new(processorCount);

            Priority = new(processorCount)
            {
                AutoRetryOnError = true,
            };
            Priority.OnWillConsume += () => Default.Suspend();
            Priority.OnDidConsume += () => Default.Resume();
        }


        /*  instance  ================================================================ */

        readonly Generator generator;
        readonly Fibers<Payload, Instruction> fibers;

        public FiberScheduler(int concurrency)
        {
            this.generator = new();
            this.fibers = new Fibers<Payload, Instruction>(concurrency, generator);

            Resume();
        }


        public int Concurrency
        {
            get => fibers.Concurrency;
            set => fibers.Concurrency = value;
        }

        public event Action? OnWillConsume;
        public event Action? OnDidConsume;

        public bool AutoRetryOnError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }


        /// <summary>
        /// > [!NOTE]
        /// > This method *won't* resume suspended scheduler.
        /// </summary>
        public void Schedule(Payload state, Func<Payload, Task<Instruction>> factory)
        {
            generator.Schedule(state, factory);

            ResumeConsumingThread();
        }


        public int RemainingTaskCount => generator.RemainingTaskCount;
        public bool IsRunning => interlock_isRunning != 0;

        volatile int interlock_isRunning;
        volatile int interlock_isThreadAlive;
        volatile TaskCompletionSource<bool>? interlock_stopper;

        /// <returns>Remaining task count</returns>
        public int Suspend()
        {
            Interlocked.Exchange(ref interlock_isRunning, 0);

            ResumeConsumingThread();  // free up thread

            return RemainingTaskCount;
        }

        public void Resume()
        {
            // always restart thread
            ResumeConsumingThread();

            if (Interlocked.Exchange(ref interlock_isRunning, 1) != 0)
            {
                return;
            }

            CreateConsumingThread();
        }


        /* =====  consuming thread  ===== */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ResumeConsumingThread()
        {
            var stopper = Interlocked.Exchange(ref interlock_stopper, null);
            stopper?.SetResult(true);
        }


        void CreateConsumingThread()
        {
            ThreadPool.UnsafeQueueUserWorkItem(static async (obj) =>
            {
                var self = (FiberScheduler)obj;
                var fibers = self.fibers;

                bool invoked_onDidConsume = true;  // true, not false
                Exception? error = null;

                if (Interlocked.Exchange(ref self.interlock_isThreadAlive, 1) != 0)
                {
                    // other thread runs
                    return;
                }

                try
                {
                RESTART:
                    // always check before consuming/restarting
                    if (self.interlock_isRunning == 0)
                    {
                        return;
                    }

                    self.OnWillConsume?.Invoke();
                    invoked_onDidConsume = false;  // set after onWill event
                    {
                        await foreach (var instruction in fibers)
                        {
                            switch (instruction)
                            {
                                case Instruction.Suspend:
                                    self.Suspend();
                                    return;

                                case Instruction.None:
                                default:
                                    break;
                            }

                            if (self.interlock_isRunning == 0)
                            {
                                return;
                            }
                        }
                    }
                    invoked_onDidConsume = true;  // set before onDid event (event may fail; avoid double execution in finally block)
                    self.OnDidConsume?.Invoke();

                    var stopper = new TaskCompletionSource<bool>();
                    if (Interlocked.CompareExchange(ref self.interlock_stopper, stopper, comparand: null) != null)
                    {
                        throw new Exception("must not be reached");
                    }

                    DEBUG($"Waiting for new task... (thread: {Environment.CurrentManagedThreadId})");

                    await stopper.Task;
                    goto RESTART;
                }
                catch (Exception e)
                {
                    error = e;

                    if (self.AutoRetryOnError)
                    {
                        if (!invoked_onDidConsume)
                        {
                            self.OnDidConsume?.Invoke();
                        }

                        _ = Task.Run(self.CreateConsumingThread);
                        return;
                    }

                    throw;
                }
                finally
                {
                    Interlocked.Exchange(ref self.interlock_isThreadAlive, 0);

                    if (!invoked_onDidConsume)
                    {
                        self.OnDidConsume?.Invoke();
                    }

                    if (error == null || !self.AutoRetryOnError)
                    {
                        self.Suspend();  // set necessary internal states
                    }

                    DEBUG($"Exiting consuming thread...");
                }
            },
            this);
        }


        /*  impl  ================================================================ */

        [StructLayout(LayoutKind.Auto)]
        public readonly struct Payload : IEquatable<Payload>
        {
            public readonly long Value;  // ex. timestamp
            public readonly object? State;

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


        public enum Instruction
        {
            None,

            /// <summary>Suspend scheduler immediately.</summary>
            Suspend,
        }


        sealed class Generator
            : IEnumerator<(Payload, Func<Payload, Task<Instruction>>)>
        {
            readonly ConcurrentQueue<(Payload state, Func<Payload, Task<Instruction>> factory)> queue = new();

            public (Payload, Func<Payload, Task<Instruction>>) Current { get; private set; }
            object IEnumerator.Current => this.Current;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }  // DO NOT clear queue here! this method is invoked when exiting foreach loop!!
            public void Reset() { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (queue.TryDequeue(out var job))
                {
                    Current = job;
                }
                else
                {
                    // ok exiting consuming thread loop
                    return false;
                }

                return true;
            }


            /*  internal  ================================================================ */

            internal int RemainingTaskCount => queue.Count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Schedule(Payload state, Func<Payload, Task<Instruction>> factory)
            {
                queue.Enqueue((state, factory));
            }
        }
    }
}
