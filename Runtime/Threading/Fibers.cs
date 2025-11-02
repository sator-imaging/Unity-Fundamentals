/** Fibers: Microthreading Library for .NET / Unity
 ** (c) 2025 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

`Fibers` is a powerful and efficient coroutine/microthreading library for C#
ported from TypeScript `ts-fibers`.

Basic Usage
===========
```cs
// create fibers based on the array
await using var fibers = Fibers.ForEach(
    concurrency: 4,
    tonsOfUrls
    async (url) =>
    {
        return await DownloadAsync(url);
    });

// iterate items in order as they complete
await foreach (var result in fibers)
{
    Console.WriteLine(result);
}
```


See Also
========
https://github.com/sator-imaging/ts-fibers

*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TaskResult = System.Byte;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    /// <summary>
    /// Represents an exception that occurs within a Fiber operation.
    /// </summary>
    public sealed class FiberException : Exception
    {
        FiberException(string message, Exception? inner) : base(message, inner) { }

        /// <summary>
        /// Throws a new <see cref="FiberException"/> with the specified message and optional inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        [DoesNotReturn]
        public static void Throw(string message, Exception? inner = null) => throw new FiberException(message, inner);
    }


    /// <summary>
    /// An abstract base class for managing concurrent asynchronous operations,
    /// often referred to as "fibers" or "lightweight threads."
    /// </summary>
    abstract public class Fibers
    {
        /// <summary>
        /// Represents a null or default task result.
        /// </summary>
        protected const TaskResult NIL = 0;

        // use byte for 8 booleans placeholder.
        /// <summary>
        /// The default task result value.
        /// </summary>
        protected const TaskResult Result_Default = 0;
        /// <summary>
        /// A task result value indicating that this code path should not be reached.
        /// </summary>
        protected const TaskResult Result_MustNotBeReached = byte.MaxValue;

        /// <summary>
        /// A non-thread-safe list to keep track of currently running tasks.
        /// </summary>
        // DO NOT USE ARRAY FOR TASK TRACKING
        protected readonly List<Task> needLock_runningTasks = new(capacity: 8);
        protected readonly object sync_runningTasks = new();

        /// <summary>
        /// The TaskCompletionSource used to manage the completion state of the fiber.
        /// </summary>
        protected readonly TaskCompletionSource<TaskResult> taskSource = new();


        /*  Properties  ================================================================ */

        /// <summary>
        /// Gets or sets an arbitrary object that can be used to store state information.
        /// </summary>
        public object? State { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }

        protected int b_concurrency;
        /// <summary>
        /// Gets or sets the maximum number of concurrent tasks allowed.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the value is less than or equal to 0.</exception>
        public int Concurrency
        {
            get => b_concurrency;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(Concurrency), $"Value must be greather than 0: {value}");
                }

                b_concurrency = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the fiber has encountered an unhandled exception.
        /// </summary>
        public bool IsFailed => this.taskSource.Task.IsFaulted;
        /// <summary>
        /// Gets a value indicating whether the fiber has completed its execution.
        /// </summary>
        public bool IsCompleted => this.taskSource.Task.IsCompleted;

        /// <summary>
        /// Gets a value indicating whether the fiber is actively processing tasks.
        /// </summary>
        abstract public bool IsRunning { get; }


        /*  Fiber Controls  ================================================================ */

        /// <summary>
        /// Stops the execution of the fiber and waits for currently running background tasks to complete.
        /// </summary>
        /// <returns>A Task that represents the asynchronous stop operation.</returns>
        abstract public Task Stop();
        /// <summary>
        /// Starts the execution of the fiber. If the fiber is already running, it returns the existing active task.
        /// </summary>
        /// <returns>A Task that represents the asynchronous start operation.</returns>
        abstract public Task Start();

        /// <summary>
        /// Returns the underlying Task that represents the asynchronous operation of the fiber.
        /// </summary>
        /// <returns>The Task associated with the fiber.</returns>
        public Task AsTask() => this.taskSource.Task;

        /// <summary>
        /// Gets an awaiter for this fiber, allowing it to be awaited.
        /// Automatically starts the fiber if it hasn't been started yet.
        /// </summary>
        /// <returns>An awaiter for the fiber's underlying Task.</returns>
        public TaskAwaiter GetAwaiter()
        {
            // auto-start to avoid indefinite loop.
            _ = Start();

            return ((Task)this.taskSource.Task).GetAwaiter();
        }


        /*  Error Handler  ================================================================ */

        /// <summary>
        /// Specifies the reason for an error occurring within a fiber.
        /// </summary>
        public enum ErrorReason
        {
            /// <summary>
            /// The error reason is unknown.
            /// </summary>
            Unknown,
            /// <summary>
            /// The error occurred during the MoveNextAsync operation.
            /// </summary>
            MoveNextAsync,
        }

        /// <summary>
        /// Defines the policy for handling errors within a fiber.
        /// </summary>
        public enum ErrorHandlingPolicy
        {
            /// <summary>
            /// Use the default error handling behavior.
            /// </summary>
            Default,
            /// <summary>
            /// Skip the current erroneous item and continue processing.
            /// </summary>
            Skip,
            /// <summary>
            /// Stop the fiber's execution upon encountering an error.
            /// </summary>
            Stop,
        }

        /// <summary>
        /// A delegate that handles exceptions occurring within the fiber.
        /// </summary>
        protected Func<Exception, Fibers, ErrorReason, ErrorHandlingPolicy>? b_errorHandler;

        /// <summary>
        /// Sets the error handler for the fiber.
        /// </summary>
        /// <param name="errorHandler">The function to call when an error occurs. It takes the exception, the fiber instance, and the error reason, and returns an <see cref="ErrorHandlingPolicy"/>.</param>
        public void SetErrorHandler(Func<Exception, Fibers, ErrorReason, ErrorHandlingPolicy>? errorHandler)
        {
            b_errorHandler = errorHandler;
        }


        /*  Factory  ================================================================ */

        /// <summary>
        /// Creates a new <see cref="Fibers{TSource, TResult}"/> instance for processing a range of integers.
        /// </summary>
        /// <typeparam name="TResult">The type of the result produced by the factory function.</typeparam>
        /// <param name="concurrency">The maximum number of concurrent tasks.</param>
        /// <param name="start">The starting value of the integer range (inclusive).</param>
        /// <param name="end">The ending value of the integer range (exclusive).</param>
        /// <param name="step">The step increment for the integer range.</param>
        /// <param name="factory">A function that takes an integer and returns a <see cref="Task{TResult}"/>.</param>
        /// <returns>A new <see cref="Fibers{TSource, TResult}"/> instance.</returns>
        public static Fibers<int, TResult> For<TResult>(int concurrency, int start, int end, int step, Func<int, Task<TResult>> factory)
        {
            return new(concurrency, generator());

            IEnumerator<(int, Func<int, Task<TResult>>)> generator()
            {
                for (int i = start; i < end; i += step)
                {
                    yield return (i, factory);
                }
            }
        }

        /// <summary>
        /// Creates a new <see cref="Fibers{TSource, TResult}"/> instance for processing a collection of items.
        /// </summary>
        /// <typeparam name="TSource">The type of the source items.</typeparam>
        /// <typeparam name="TResult">The type of the result produced by the factory function.</typeparam>
        /// <param name="concurrency">The maximum number of concurrent tasks.</param>
        /// <param name="items">The collection of items to process.</param>
        /// <param name="factory">A function that takes an item of type <typeparamref name="TSource"/> and returns a <see cref="Task{TResult}"/>.</param>
        /// <returns>A new <see cref="Fibers{TSource, TResult}"/> instance.</returns>
        public static Fibers<TSource, TResult> ForEach<TSource, TResult>(int concurrency, IEnumerable<TSource> items, Func<TSource, Task<TResult>> factory)
        {
            return new(concurrency, generator());

            IEnumerator<(TSource, Func<TSource, Task<TResult>>)> generator()
            {
                foreach (var item in items)
                {
                    yield return (item, factory);
                }
            }
        }
    }


    /// <summary>
    /// Represents a concrete implementation of <see cref="Fibers"/> that processes items from a generator
    /// and yields results asynchronously.
    /// </summary>
    /// <typeparam name="TSource">The type of the source items processed by the fiber.</typeparam>
    /// <typeparam name="TValue">The type of the result value produced by each task.</typeparam>
    public class Fibers<TSource, TValue>
        : Fibers
        , IAsyncEnumerable<TValue>
        , IAsyncEnumerator<TValue>
    {
        readonly IEnumerator<(TSource source, Func<TSource, Task<TValue>> factory)> generator;

        /// <summary>
        /// Initializes a new instance of the <see cref="Fibers{TSource, TValue}"/> class.
        /// </summary>
        /// <param name="concurrency">The maximum number of concurrent tasks.</param>
        /// <param name="generator">The enumerator that provides source items and their corresponding factory functions.</param>
        public Fibers(
            int concurrency,
            IEnumerator<(TSource source, Func<TSource, Task<TValue>> factory)> generator
        )
        {
            this.Concurrency = concurrency;  // use setter to validate argument
            this.generator = generator;

            this.Current = (((default)))!;
        }


        /*  Start/Stop  ================================================================ */

        // NOTE: there are 2 ways to consume fibers, `Start()` and `await foreach`.
        //       to achieve thread-safe state handling easily, use task for both usecase.
        //       * no matter whether it is completed or not
        readonly Task ConsumingTasksByForeach = Task.Run(static () => { });  // don't use Task ctor (created task has special internal state)

        volatile Task? interlock_activeConsumingTask;

        /// <inheritdoc/>
        public override bool IsRunning
        {
            // field value change may not be visible in all threads immediately so need to check task completion also
            get => interlock_activeConsumingTask != null && !this.taskSource.Task.IsCompleted;
        }

        /// <inheritdoc/>
        public override Task Stop()
        {
            var spinWait = new SpinWait();

            var active = interlock_activeConsumingTask;
            do
            {
                if (active == ConsumingTasksByForeach)
                {
                    FiberException.Throw("Attempting to stop fibers running by `await foreach`");
                }

                var previous = Interlocked.CompareExchange(ref interlock_activeConsumingTask, null, active);
                if (previous == active)
                {
                    return active ?? Task.CompletedTask;
                }

                active = previous;

                spinWait.SpinOnce();
            }
            while (true);
        }

        /// <exception cref="FiberException">Thrown if an attempt is made to start the fiber while it is being iterated over asynchronously.</exception>
        /// <inheritdoc/>
        public override Task Start()
        {
            var activeTaskSource = new TaskCompletionSource<TaskResult>();

            var previous = Interlocked.CompareExchange(ref interlock_activeConsumingTask, activeTaskSource.Task, null);
            if (previous != null)
            {
                if (previous == ConsumingTasksByForeach)
                {
                    FiberException.Throw("Cannot start while iterating over fibers");
                }

                return previous;  // other thread takes control
            }

            return impl(this, activeTaskSource);

            static async Task impl(Fibers<TSource, TValue> self, TaskCompletionSource<TaskResult> activeTaskSource)
            {
                var activeTask = activeTaskSource.Task;

                try
                {
                    do   // perf
                    {
                    }
                    while (
                        self.interlock_activeConsumingTask == activeTask &&
                        await self.MoveNextAsync()
                    );
                }
                finally
                {
                    activeTaskSource.TrySetResult(NIL);

                    _ = Interlocked.CompareExchange(ref self.interlock_activeConsumingTask, null, activeTask);
                }
            }
        }


        /*  IAsyncEnumerable/Enumerator  ================================================================ */

        /// <summary>
        /// Gets the element in the collection at the current position of the enumerator.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public TValue Current { get; private set; }

        /// <summary>
        /// Returns an enumerator that iterates asynchronously through the collection.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that may be used to cancel the asynchronous iteration.</param>
        /// <returns>An enumerator that can be used to iterate asynchronously through the collection.</returns>
        /// <exception cref="FiberException">Thrown if an attempt is made to iterate while the fiber is running in the background.</exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public IAsyncEnumerator<TValue> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            var active = Interlocked.CompareExchange(ref interlock_activeConsumingTask, ConsumingTasksByForeach, null);
            if (active != null)
            {
                if (active == ConsumingTasksByForeach)
                {
                    FiberException.Throw("Cannot iterate while other thread is consuming tasks");
                }

                FiberException.Throw("Cannot iterate while task is running in background");
            }

            return this;
        }


        /// <summary>
        /// Advances the enumerator asynchronously to the next element of the collection.
        /// </summary>
        /// <returns>A <see cref="ValueTask{Boolean}"/> that will complete with a value of <c>true</c> if the enumerator was successfully advanced to the next element; <c>false</c> if the enumerator has passed the end of the collection.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public async ValueTask<bool> MoveNextAsync()
        {
            bool loopFinished = false;
            Task? activeTask = null;

            var generator = this.generator;
            var concurrency = this.b_concurrency;
            var needLock_runningTasks = this.needLock_runningTasks;
            var sync_runningTasks = this.sync_runningTasks;

            try
            {
                this.Current = (((default)))!;  // always reset!

                int taskCount;
                while ((taskCount = needLock_runningTasks.Count) < concurrency)
                {
                    if (!generator.MoveNext())
                    {
                        break;
                    }

                    var (source, factory) = generator.Current;
                    activeTask = factory.Invoke(source);

                    lock (sync_runningTasks)
                    {
                        needLock_runningTasks.Add(activeTask);
                    }
                }

                if (taskCount != 0)
                {
                    // NOTE: for thread-safety, MUST clone array.
                    //       --> Task.WhenAny does so correctly, not required to do here.
                    // https://github.com/dotnet/runtime/blob/v5.0.0/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L5936
                    // https://github.com/microsoft/referencesource/blob/4.6.2/Microsoft.Bcl.Async/Microsoft.Threading.Tasks/Threading/Tasks/TaskEx.cs#L314-L386

                    // lock while making defensive copy.
                    Task<Task> whenAny;
                    lock (sync_runningTasks)
                    {
                        // ofcourse, DO NOT await task in lock scope!
                        whenAny = Task.WhenAny(needLock_runningTasks);
                    }

                    activeTask = await whenAny;

                    this.Current = await ((Task<TValue>)activeTask);
                    return true;
                }
                else
                {
                    loopFinished = true;
                }
            }
            catch (Exception error)
            {
            UNWRAP:
                if (error is AggregateException aggregate &&
                    aggregate.InnerExceptions.Count == 1)
                {
                    error = aggregate.InnerExceptions[0];
                    goto UNWRAP;
                }

                switch (b_errorHandler?.Invoke(error, this, ErrorReason.MoveNextAsync))
                {
                    case ErrorHandlingPolicy.Stop:
                        loopFinished = true;
                        break;  // must return AFTER finally block.

                    case ErrorHandlingPolicy.Skip:
                        break;  // must return AFTER finally block.

                    case ErrorHandlingPolicy.Default:
                    case null:
                    default:
                        await RaiseErrorAndDispose(error);
                        throw error;
                }
            }
            finally
            {
                if (activeTask != null)
                {
                    lock (sync_runningTasks)
                    {
                        needLock_runningTasks.Remove(activeTask);
                    }
                }
            }

            if (loopFinished)
            {
                await ResolveIfRequiredAndDispose();
                return false;
            }
            else
            {
                return await MoveNextAsync();
            }
        }

        ValueTask RaiseErrorAndDispose(Exception error)
        {
            this.taskSource.TrySetException(error);

            return DisposeAsync();
        }

        ValueTask ResolveIfRequiredAndDispose()
        {
            this.taskSource.TrySetResult(Result_Default);

            return DisposeAsync();
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources asynchronously.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ValueTask DisposeAsync()
        {
            this.taskSource.TrySetResult(Result_MustNotBeReached);
            this.needLock_runningTasks.Clear();  // ok without lock

            this.generator.Dispose();

            // Should consider Fibers may be started by whether Start() or await foreach.
            Interlocked.Exchange(ref interlock_activeConsumingTask, null);

            return default;
        }
    }
}
