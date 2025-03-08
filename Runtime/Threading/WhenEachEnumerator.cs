/** `WhenEach` for Unity / .NET Standard 2.1
 ** (c) 2025 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

How to Use
==========
```cs
// available for Task[], ReadOnlySpan<Task>, ICollection<Task> and IEnumerable<Task>
await foreach (var task in tasks.WhenEach())
{
    // do something for completed task
}
```

 */

using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Random = System.Random;

#nullable enable

#if NET9_0 == false

namespace SatorImaging.UnityFundamentals
{
    /// <summary>
    /// Extension methods for <see cref="WhenEachEnumerator{T}"/>.
    /// </summary>
    public static class WhenEachEnumeratorExtensions
    {
        public static WhenEachEnumerator<T> WhenEach<T>(this T[] tasks, CancellationToken cancellationToken = default)
            where T : Task => new(tasks, cancellationToken);

        public static WhenEachEnumerator<T> WhenEach<T>(this ReadOnlySpan<T> tasks, CancellationToken cancellationToken = default)
            where T : Task => new(tasks, cancellationToken);

        public static WhenEachEnumerator<T> WhenEach<T>(this ICollection<T> tasks, CancellationToken cancellationToken = default)
            where T : Task => new(tasks, cancellationToken);

        public static WhenEachEnumerator<T> WhenEach<T>(this IEnumerable<T> tasks, CancellationToken cancellationToken = default)
            where T : Task => new(tasks, cancellationToken);
    }


    /// <summary>
    /// <c>Task.WhenEach</c> for Unity / .NET Standard 2.1
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct WhenEachEnumerator<T>
        : IAsyncEnumerator<T>
        , IAsyncEnumerable<T>
        where T : Task
    {
        readonly static ConcurrentStack<byte[]> cache_remaining = new();

        readonly T[] tasks;
        readonly byte[] remaining;  // need to use array to make struct readonly
        readonly CancellationToken ct;

        WhenEachEnumerator(int length, CancellationToken ct)
        {
            if (!cache_remaining.TryPop(out remaining))
            {
                remaining = new byte[length];
            }

            this.remaining[0] = checked((byte)length);
            this.tasks = ArrayPool<T>.Shared.Rent(length);  // must be done after bounds check
            this.ct = ct;
        }

        public WhenEachEnumerator(T[] array, CancellationToken ct) : this(array.Length, ct) => array.AsSpan().CopyTo(this.tasks);
        public WhenEachEnumerator(ReadOnlySpan<T> span, CancellationToken ct) : this(span.Length, ct) => span.CopyTo(this.tasks);
        public WhenEachEnumerator(ICollection<T> collection, CancellationToken ct) : this(collection.Count, ct) => collection.CopyTo(this.tasks, 0);

        public WhenEachEnumerator(IEnumerable<T> enumerable, CancellationToken ct)
            : this(16, ct)  // NOTE: first, try with enough size in most cases.
                            //       if larger, return rental buffer and retry with actual size.
        {
        RETRY:
            bool isOverflow = false;
            int lastIndex = this.tasks.Length - 1;

            int length = -1;
            foreach (var task in enumerable)
            {
                length++;
                if (length > lastIndex)
                {
                    isOverflow = true;
                    continue;  // don't break here! continue counting up!!
                }

                this.tasks[length] = task;
            }

            if (isOverflow)
            {
                ArrayPool<T>.Shared.Return(this.tasks, clearArray: true);  // must clear!

                // throw after returning rental array
                if (length > byte.MaxValue)
                {
                    cache_remaining.Push(this.remaining);
                    throw new ArgumentOutOfRangeException("so many tasks: " + length);
                }

                this.tasks = ArrayPool<T>.Shared.Rent(length);
                goto RETRY;
            }

            this.remaining[0] = checked((byte)length);
            this.ct = ct;
        }


        public async ValueTask DisposeAsync()
        {
            this.remaining[0] = 0;

            ArrayPool<T>.Shared.Return(this.tasks, clearArray: true);  // must clear!
            cache_remaining.Push(this.remaining);
        }


        public IAsyncEnumerator<T> GetAsyncEnumerator() => this;
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct)
        {
            var backup = this.tasks;
            this.DisposeAsync();

            return new WhenEachEnumerator<T>(backup, ct);
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

            _ = Task.Run(async () =>
            {
                var rng = new Random();
                var jobInfo = new[] {
                    (1, rng.Next(310, 3100)),
                    (2, rng.Next(310, 3100)),
                    (3, rng.Next(310, 3100)),
                };
                var expect = jobInfo.OrderBy(x => x.Item2).ToArray();

                var tasks = new Task<int>[jobInfo.Length];
                for (int i = 0; i < jobInfo.Length; i++)
                {
                    var info = jobInfo[i];
                    tasks[i] = SimpleJob(info.Item1, info.Item2);
                }

                var start = Stopwatch.GetTimestamp();
                UnityEngine.Debug.Log("= WhenEachEnumerator ===");

                int e = -1;
                await foreach (var task in tasks.WhenEach())
                {
                    e++;
                    // assertion doesn't show error if runs in thread pool...!
                    if (task.Result != expect[e].Item1)
                    {
                        var msg = $"[FAILED] Expect: {expect[e].Item1} / Actual: {task.Result}";
                        UnityEngine.Debug.LogError(msg);
                        throw new Exception(msg);
                    }

                    var elapsedMillis = TimeSpan.FromTicks((long)((Stopwatch.GetTimestamp() - start) * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency))).TotalMilliseconds;
                    UnityEngine.Debug.Log($"-> {task.Result}: " + elapsedMillis);
                }
            });


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        static async Task<int> SimpleJob(int jobNumber, int delay)
        {
            await Task.Delay(delay);
            UnityEngine.Debug.Log($"No.{jobNumber}: {delay} ms");
            return jobNumber;
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

#endif
