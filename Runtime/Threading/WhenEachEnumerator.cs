/** `Task.WhenEach` for Unity / .NET Standard 2.1
 ** (c) 2025 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

How to Use
==========
```cs
await foreach (var task in tasks.WhenEach())
{
    // do something for completed task
}

// for efficiency, cancellation token should be passed to WhenEach() directly.
// note that cancellation affects only on enumeration.
// jobs may continue running if those are depending on different token.
await foreach (var task in tasks.WhenEach(ct)) { }

// WithCancellation() creates new struct so a little bit inefficient.
await foreach (var task in tasks.WhenEach().WithCancellation(ct)) { }
```

 */

using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Random = System.Random;

#nullable enable

//#if NET9_0 == false

namespace SatorImaging.UnityFundamentals
{
    /// <summary>
    /// Extension methods for <see cref="WhenEachEnumerator{T}"/>.
    /// </summary>
    public static class WhenEachEnumeratorExtensions
    {
        // NOTE: assume that tasks list is always small sized so no difference between these overloads on performance.
        //       performance lovers can use `new WhenEachEnumerator(ReadOnlySpan<T>)` explicitly.

        ///// <inheritdoc cref="WhenEachEnumerator{T}"/>
        //public static WhenEachEnumerator<T> WhenEach<T>(this T[] tasks, CancellationToken cancellationToken = default)
        //    where T : Task => new(tasks.AsSpan(), cancellationToken);

        ///// <inheritdoc cref="WhenEachEnumerator{T}"/>
        //public static WhenEachEnumerator<T> WhenEach<T>(this Span<T> tasks, CancellationToken cancellationToken = default)
        //    where T : Task => new(tasks, cancellationToken);

        ///// <inheritdoc cref="WhenEachEnumerator{T}"/>
        //public static WhenEachEnumerator<T> WhenEach<T>(this ReadOnlySpan<T> tasks, CancellationToken cancellationToken = default)
        //    where T : Task => new(tasks, cancellationToken);


        /// <inheritdoc cref="WhenEachEnumerator{T}"/>
        public static WhenEachEnumerator<T> WhenEach<T>(this ICollection<T> tasks, CancellationToken cancellationToken = default)
            where T : Task => new(tasks, cancellationToken);


        /// <inheritdoc cref="WhenEachEnumerator{T}"/>
        public static WhenEachEnumerator<Task> WhenEach(this IEnumerable<Task> tasks, CancellationToken cancellationToken = default)
            => new(tasks, cancellationToken);
    }


    [StructLayout(LayoutKind.Auto)]
    public readonly struct WhenEachEnumerator<T>
        : IAsyncEnumerator<T>
        , IAsyncEnumerable<T>
        where T : Task
    {
        internal const int DEFAULT_ENUMERABLE_COUNT = 16;

        [ThreadStatic] static int[]? ts_remaining;

        readonly T[] tasks;
        readonly int[] remaining;  // need to use array to make struct readonly
        readonly CancellationToken ct;

        WhenEachEnumerator(int length, CancellationToken ct)
        {
            if (ts_remaining == null)
            {
                this.remaining = new int[1];
            }
            else
            {
                this.remaining = ts_remaining;
                ts_remaining = null;
            }

            this.remaining[0] = length;
            this.tasks = length == 0 ? Array.Empty<T>() : ArrayPool<T>.Shared.Rent(length);
            this.ct = ct;
        }

        public WhenEachEnumerator(ReadOnlySpan<T> span, CancellationToken ct) : this(span.Length, ct) => span.CopyTo(this.tasks);
        public WhenEachEnumerator(ICollection<T> collection, CancellationToken ct) : this(collection.Count, ct) => collection.CopyTo(this.tasks, 0);

        public WhenEachEnumerator(IEnumerable<T> enumerable, CancellationToken ct)
            : this(DEFAULT_ENUMERABLE_COUNT, ct)  // NOTE: first, try with enough size for most cases.
                                                  //       if larger, return rental buffer and retry with actual size.
        {
        RETRY:
            int lastIndex = this.tasks.Length - 1;

            int currentIndex = -1;
            foreach (var task in enumerable)
            {
                currentIndex++;
                if (currentIndex > lastIndex)
                {
                    continue;  // don't break here! continue counting up!!
                }

                this.tasks[currentIndex] = task;
            }

            int totalCount = currentIndex + 1;
            if (currentIndex > lastIndex)
            {
                DisposeCore();

                this.tasks = ArrayPool<T>.Shared.Rent(totalCount);
                goto RETRY;
            }

            this.remaining[0] = totalCount;
            this.ct = ct;
        }


        void DisposeCore()
        {
            this.remaining[0] = 0;

            if (this.tasks.Length != 0)
            {
                ArrayPool<T>.Shared.Return(this.tasks, clearArray: true);  // must clear!
            }

            ts_remaining = this.remaining;
        }

        public async ValueTask DisposeAsync()
        {
            DisposeCore();
        }


        public IAsyncEnumerator<T> GetAsyncEnumerator() => this;
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct)
        {
            // NOTE: array may different size from actual count of tasks. slice it!!
            var result = new WhenEachEnumerator<T>(this.tasks.AsSpan(0, this.remaining[0]), ct);

            DisposeCore();
            return result;
        }


        // perf: move completed task to the end of array and return it to
        //       avoid checking indices of completed tasks in every iteration.
        public T Current => tasks[remaining[0]];


        public async ValueTask<bool> MoveNextAsync()
        {
            var remaining = this.remaining[0];

            T? result;
            while (remaining != 0)
            {
                if (this.ct.IsCancellationRequested)
                {
                    this.remaining[0] = 0;
                    return false;
                }

                // TODO: use Span<T> in C# 13.0
                //var tasks = this.tasks.AsSpan();
                for (int i = 0; i < remaining; i++)
                {
                    result = tasks[i];
                    if (result.IsCompleted)
                    {
                        remaining--;
                        if (i != remaining)
                        {
                            tasks[i] = tasks[remaining];
                            tasks[remaining] = result;
                        }

                        this.remaining[0] = remaining;
                        return true;
                    }
                }

                await Task.Yield();
            }

            return false;
        }
    }
}




#region ////////  TEMPLATE: Unity Editor Tests  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006  // naming style
#pragma warning disable CA1861   // avoid constant array

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.TEST.WhenEach_Enumerator  // must be unique. don't reuse existing namespace
{
    static class UNITY_EDITOR_TESTS  // don't change
    {
        const string MENU_ROOT = nameof(TEST) + "/" + nameof(WhenEach_Enumerator) + "/";


        #region ////////  TEMPLATE: Test Methods  ////////
        /*  TEMPLATE: Test Methods  ================================================================ */

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Basic_Tests), priority = 0)]
        [Test]
        static void Basic_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            // cannot run simlutaneously, because of cancellation tests
            _ = Task.Run(async () =>
            {
                try
                {
                    await Test(0);
                    await Test(1);
                    await Test(2);
                }
                catch (Exception exc)
                {
                    UnityEngine.Debug.LogException(exc);
                    throw;
                }


                // TEST: done!!
                UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
            });


            /* =====  nested methods  ===== */

            static Task Test(int mode)
            {
                return Task.Run(async () =>
                {
                    try
                    {
                        var rng = new Random();
                        var jobInfo = new[] {
                            (1, rng.Next(310, 3100)),
                            (2, rng.Next(310, 3100)),
                            (3, rng.Next(310, 3100)),
                        }
                        // to make test stable, shuffle job number but delay keeps consistent
                        .OrderBy(x => x.Item2)
                        .Select((x, i) => (x.Item1, 1000 + 1000 * i))
                        .ToArray();

                        var cts = new CancellationTokenSource();

                        var tasks = new Task<int>[jobInfo.Length];
                        for (int i = 0; i < jobInfo.Length; i++)
                        {
                            var info = jobInfo[i];
                            tasks[i] = SimpleJob(info.Item1, info.Item2, cts);
                        }

                        var startTimestamp = Stopwatch.GetTimestamp();
                        UnityEngine.Debug.Log("= WhenEachEnumerator ===");

                        int e = -1;
                        int expectedCompletionCount;
                        switch (mode)
                        {
                            case 0:
                                await foreach (Task<int> task in tasks.WhenEach())
                                {
                                    e++;
                                    Report(startTimestamp, jobInfo[e].Item1, task.Result);

                                    cts.Cancel();
                                }

                                e++;
                                CheckCompletedTaskCount(3, e);

                                break;

                            case 1:
                                await foreach (Task<int> task in tasks.WhenEach(cts.Token))
                                {
                                    e++;
                                    Report(startTimestamp, jobInfo[e].Item1, task.Result);

                                    cts.Cancel();
                                }

                                e++;
                                CheckCompletedTaskCount(1, e);

                                break;

                            case 2:
                                await foreach (Task<int> task in tasks.WhenEach().WithCancellation(cts.Token))
                                {
                                    e++;
                                    Report(startTimestamp, jobInfo[e].Item1, task.Result);

                                    cts.Cancel();
                                }

                                e++;
                                CheckCompletedTaskCount(1, e);

                                break;
                        }

                        UnityEngine.Debug.Log("DONE");
                    }
                    catch (Exception exc)
                    {
                        UnityEngine.Debug.LogException(exc);
                        throw;
                    }
                });
            }

            static void Report(long startTimestamp, int expect, int actual)
            {
                // assertion doesn't show error if runs in thread pool...!
                if (actual != expect)
                {
                    throw new Exception($"Expect: {expect} / Actual: {actual}");
                }

                var elapsedMillis = TimeSpan.FromTicks((long)((Stopwatch.GetTimestamp() - startTimestamp) * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency))).TotalMilliseconds;
                UnityEngine.Debug.Log($"-> {actual}: " + elapsedMillis);
            }

            static void CheckCompletedTaskCount(int expect, int actual)
            {
                if (expect != actual)
                {
                    throw new Exception($"Completed task count is not {expect}: {actual}");
                }
            }

            static async Task<int> SimpleJob(int jobNumber, int delay, CancellationTokenSource cts)
            {
                await Task.Delay(delay);
                UnityEngine.Debug.Log($"No.{jobNumber}: {delay} ms");

                return jobNumber;
            }
        }



        [UnityEditor.MenuItem(MENU_ROOT + nameof(Error_Tests), priority = 0)]
        [Test]
        static void Error_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (Task<int> task in new Task[] { Task<int>.CompletedTask, Task<float>.CompletedTask }.WhenEach())
                    {
                    }
                }
                catch (Exception exc)
                {
                    if (exc is not InvalidCastException || exc.Message != "Specified cast is not valid.")
                    {
                        UnityEngine.Debug.LogException(exc);
                        throw;
                    }
                }


                // TEST: done!!
                UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
            });
        }


        [UnityEditor.MenuItem(MENU_ROOT + nameof(Ctor_IEnumerable), priority = 0)]
        [Test]
        static void Ctor_IEnumerable()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            // there was a chance to loop indefinitely when task count is 2^n+1 (n>=4)

            _ = Task.Run(async () =>
            {
                try
                {
                    int testSize = WhenEachEnumerator<Task>.DEFAULT_ENUMERABLE_COUNT;
                    int count = 0;
                    await foreach (var x in (Enumerable.Range(0, testSize).Select(_ => Task.CompletedTask)).WhenEach())
                    {
                        count++;
                    }
                    if (count != testSize)
                        throw new Exception($"completed task count is not {testSize}: {count}");

                    testSize++;
                    count = 0;
                    await foreach (var x in (Enumerable.Range(0, testSize).Select(_ => Task.CompletedTask)).WhenEach())
                    {
                        count++;
                    }
                    if (count != testSize)
                        throw new Exception($"completed task count is not {testSize}: {count}");
                }
                catch (Exception exc)
                {
                    UnityEngine.Debug.LogException(exc);
                    throw;
                }


                // TEST: done!!
                UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
            });
        }


        /*  TEMPLATE: End of Tests  ================================================================ */
        #endregion    //  TEMPLATE: End of Tests


        /* TEMPLATE: add 'using NUnit.Framework;' to header of script to fix error */

        /* TEMPLATE: copy & paste and replace argument for 'nameof()'

        [UnityEditor.MenuItem(MENU_ROOT + nameof(__Underscore_Separated_Method_Name__), priority = 0)]
        [Test]
        static void Basic_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }

        */


        // TEMPLATE: run all tests in this class
        [UnityEditor.MenuItem(MENU_ROOT + "Run All Tests", priority = int.MinValue + 310)]
        static void UnityEditorTests_RunAllTests()
        {
            const int REPEAT_COUNT = 3;

            foreach (var method in UnityEditor.TypeCache.GetMethodsWithAttribute<TestAttribute>())
            {
                if (method.DeclaringType != typeof(UNITY_EDITOR_TESTS))
                    continue;

                for (var i = 1; i <= REPEAT_COUNT; i++)
                {
                    UnityEngine.Debug.Log($"=======   {method.Name}  Repeat: {i}/{REPEAT_COUNT}   =======");
                    method.Invoke(null, null);
                }
            }

            UnityEngine.Debug.Log(@"  <b>\\\  Tests Passed  ///</b>  " + typeof(UNITY_EDITOR_TESTS).Namespace);
        }

        // TEMPLATE: open script file
        [UnityEditor.MenuItem(MENU_ROOT + "Edit Tests...", priority = int.MaxValue - 310)]
        static void UnityEditorTests_EditTests() => __EditTests();

        static void __EditTests(
            [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
    }
}
#endif
#endregion

//#endif
