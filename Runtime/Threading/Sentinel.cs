/** Efficient UI Event and Thread Manager for .NET / Unity
 ** (c) 2025 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

With design eliminating unnecessary lock operations, `Sentinel` provides extreme fast and
efficient way to manage exclusive and/or concurrent tasks and threads.

Basic Usage
===========
```cs
readonly SentinelToken _token = Sentinel.GetUniqueToken();

// use `ExclusiveScope()` method to define exclusive operation block
using (Sentinel.ExclusiveScope(_token, out var rejected)
{
    if (rejected) return;

    // do the exclusive task
}

// use `DebounceScope()` with blockless-using pattern
using var _ = Sentinel.DebounceScope(_token, 3, out var rejected);

if (rejected)
    return;  // only first 3 thread or event can enter
```


Async Callback Handling
-----------------------
Even if callback is invoked only on main thread, event handler may run multiple times if it is marked `async`.
(ex. multiple button clicks may invoke multiple callbacks)

Here shows how to prevent multiple event invocations in single thread app.

```cs
// technically, async method is immediately finished so there is chance to run multiple times
myEvent.Subscribe(async () =>
{
    using (Sentinel.SingleThreadScope(_token, out var entrantCount)
    {
        // check current entrant count
        if (entrantCount != 0)
            return;

        await FooAsync();
        await Task.Delay(1000);
    }
});
```


Advanced Usage
==============
`Sentinel` providing features to eliminate insane Rx operator chains.

UI Event Handling
-----------------
Make `ThrottleFirst` or `Debounce` in modern C# style.

```cs
myButton.onClick += async () =>
{
    // use shared token to allow only a button can work at a moment
    using (Sentinel.ExclusiveScope(_sharedTokenAcrossButtons, out var rejected)
    {
        // other buttons won't work until this event has finished
        if (rejected) return;

        myButton.enable = false;
        try
        {
            OnMyButtonClick();

            // only allow click once in second, to achieve balancing event stream
            await Task.Delay(1000);
        }
        finally
        {
            myButton.enable = true;
        }

        // reaches here after Task.Delay operation is finished.
        // and automatically free up exclusive lock by IDisposable.
    }
};
```


Retry Operation
---------------
You can write better UX code more simple way rather than using Rx operator chaining.

```cs
// case 1) retry entering exclusive scope
RETRY_ENTER:
    using (Sentinel.ExclusiveScope(_token, out var rejected)
    {
        if (rejected)
        {
            await Task.Delay(100);
            goto RETRY_ENTER;
        }

        // case 2) retry network access
        int retryCount = 0;
        int delay = 1000;
        while (true)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                if (retryCount > 0)
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);

                    // can easily implement exponential backoff
                    delay *= 2;
                }

                await myNetworkClient.GetAsync(something, timeout: 3000, ct);
                break;
            }
            catch (TimeoutException)
            {
                retryCount++;

                if (retryCount > 10)
                    throw;

                // achieve better app UX without insane Rx techniques
                if (retryCount > 3)
                    ShowToastNotification("Server now gets many traffic. Thank you for your patience.");
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
```


Extendable Delay Operation
--------------------------
Here shows how to achieve "extendable wait" in UI events or multi-threaded functions.
(ex. invoke callback after a second since slider dragging is finished)

```cs
private int m_waitDuration;
private int m_latestValue;

// event/thread always updates wait duration even if cannot enter exclusive block to
// prevent event invocation. as a result, event listener invokes only once when a second
// elapsed since last event arrival.
void OnChanged(int value)
{
    // always set!!
    m_waitDuration = 1000;
    m_latestValue = value;

    using (Sentinel.ExclusiveScope(_token, out var rejected)
    {
        if (rejected) return;

        // check frequency in milliseconds
        const int freq = 100;

        // this delay continues until event stream stops.
        while ((m_waitDuration -= freq) > 0)
        {
            await Task.Delay(freq, ct).ConfigureAwait(false);
        }

        // reaches here a second later since last event.
        DelayedAction(m_latestValue);
    }
}
```


`Sentinel` provides helper method to achieve more accurate delay.

```cs
// use timestamp instead of wait duration
private long m_startTimestamp;

// in event listener, repeat delay until time has elapsed
int remaining;
while ((remaining = Sentinel.GetRemainingMilliseconds(m_startTimestamp, 1000)) > 0)
{
    await Task.Delay(remaining, ct).ConfigureAwait(false);
}
```


Technical Notes
===============
If you have encountered error related on `SentinelToken`, define preprocessor directive
`#define STMG_SENTINEL_ENABLE_STRICT_TYPEDEF` can solve the problem.

 */

//#define STMG_SENTINEL_ENABLE_STRICT_TYPEDEF

using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace SatorImaging.UnityFundamentals
{

#if STMG_SENTINEL_ENABLE_STRICT_TYPEDEF
    [StructLayout(LayoutKind.Auto)]
    public readonly struct SentinelToken
    {
        readonly short token;

        internal SentinelToken(int token) => this.token = checked((short)token);

        public static explicit operator SentinelToken(int token) => new(token);
        public static explicit operator short(SentinelToken self) => self.token;
    }
#else
    // typedef: aliasing 'short' as 'Token'. crazy!!
    /// <summary>Use <see cref="Sentinel.GetUniqueToken"/> to get correct token.</summary>
    public enum SentinelToken : short { }
#endif


    /// <summary>
    /// The thread watch.
    /// </summary>
    public static class Sentinel
    {
        volatile static int _issuedToken = short.MinValue;  // short!! Interlocked doesn't support short!!

        /// <summary>Get unique token for current app session.</summary>
        public static SentinelToken GetUniqueToken()
        {
            var token = _issuedToken;
            if (token > short.MaxValue)
            {
                ThrowInvalidOperation("issued token count exceeds limit: " + short.MaxValue);
            }

            Interlocked.Increment(ref _issuedToken);

            // warmup dictionary on startup
            sentinel_debounce.GetOrAdd((short)token, cache_CreateDebounceState);

            return (SentinelToken)token;
        }


        /*  exceptions  ================================================================ */

        [DoesNotReturn] static void ThrowArgumentOutOfRange(string paramName) => throw new ArgumentOutOfRangeException(paramName);
        [DoesNotReturn] static void ThrowInvalidOperation(string message) => throw new InvalidOperationException(message);


        /*  helpers  ================================================================ */

        // https://github.com/dotnet/runtime/blob/v9.0.2/src/libraries/Microsoft.Extensions.Http/src/ValueStopwatch.cs#L11
        readonly static double s_timestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        /// <summary>Get remaining milliseconds since start timestamp.</summary>
        /// <param name="startingTimestamp">Use <see cref="Stopwatch.GetTimestamp"/>.</param>
        /// <returns>may be negative value</returns>
        public static int GetRemainingMilliseconds(long startingTimestamp, int waitDurationMilliseconds)
        {
            var now = Stopwatch.GetTimestamp();
            var elapsedTime = new TimeSpan((long)((now - startingTimestamp) * s_timestampToTicks));
            return (int)(waitDurationMilliseconds - elapsedTime.TotalMilliseconds);
        }


        /*  Exclusive  ================================================================ */

        /// <inheritdoc cref="DebounceScope(SentinelToken, int, out bool)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebounceDisposable ExclusiveScope(SentinelToken token, out bool rejected) => DebounceScope(token, 1, out rejected);

        /// <inheritdoc cref="CanEnterDebounce(SentinelToken, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanEnterExclusive(SentinelToken token) => CanEnterDebounce(token, 1);

        /// <inheritdoc cref="EnterDebounce(SentinelToken, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EnterExclusive(SentinelToken token) => EnterDebounce(token, 1);

        /// <inheritdoc cref="ExitDebounce(SentinelToken, bool)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExitExclusive(SentinelToken token, bool throwIfEntrantCountIsLowerThanExit = true)
            => ExitDebounce(token, throwIfEntrantCountIsLowerThanExit);


        /*  Debounce  ================================================================ */

        readonly static ConcurrentDictionary<short, DebounceState> sentinel_debounce = new();
        readonly static Func<short, DebounceState> cache_CreateDebounceState = (_) => new();

        // NOTE: wrapping primitive object with reference type will allow significantly reducing lockdown time.
        internal sealed class DebounceState
        {
            volatile public int Count;
        }


        public static bool CanEnterDebounce(SentinelToken token, int count)
        {
            if (count <= 0)
                ThrowArgumentOutOfRange(nameof(count));

            var key = (short)token;
            var state = sentinel_debounce.GetOrAdd(key, cache_CreateDebounceState);

            return state.Count < count;
        }


        public static bool EnterDebounce(SentinelToken token, int count)
        {
            _ = DebounceScope(token, count, out var rejected);
            return !rejected;
        }

        public static void ExitDebounce(SentinelToken token, bool throwIfEntrantCountIsLowerThanExit = true)
        {
            var key = (short)token;
            var state = sentinel_debounce.GetOrAdd(key, cache_CreateDebounceState);

            Interlocked.Decrement(ref state.Count);

            if (state.Count < 0)
            {
                Interlocked.Exchange(ref state.Count, 0);

                if (throwIfEntrantCountIsLowerThanExit)
                {
                    ThrowInvalidOperation("enter and exit count doesn't match");
                }
            }
        }


        public static DebounceDisposable DebounceScope(SentinelToken token, int count, out bool rejected)
        {
            if (count <= 0)
                ThrowArgumentOutOfRange(nameof(count));

            var key = (short)token;
            var state = sentinel_debounce.GetOrAdd(key, cache_CreateDebounceState);

            if (state.Count >= count)
            {
                rejected = true;
                return new(null);
            }

            Interlocked.Increment(ref state.Count);

            rejected = false;
            return new(state);
        }


        [StructLayout(LayoutKind.Auto)]
        public readonly struct DebounceDisposable : IDisposable
        {
            readonly DebounceState? state;

            internal DebounceDisposable(DebounceState? state) => this.state = state;

            public void Dispose()
            {
                if (state == null)
                    return;

                Interlocked.Decrement(ref state.Count);
            }
        }


        /*  single thread operations  ================================================================ */

        internal sealed class SingleThreadState
        {
            public byte Count;
        }
        readonly static Dictionary<short, SingleThreadState> sentinel_singleThread = new();

        /// <summary>
        /// > [!NOTE]
        /// > This method won't perform interlocked increment/decrement operation.
        /// </summary>
        public static SingleThreadDisposable SingleThreadScope(SentinelToken token, out byte currentEntrantCount)
        {
            var key = (short)token;
            var dict = sentinel_singleThread;

            if (!dict.TryGetValue(key, out var state))
            {
                state = new();
                dict.Add(key, state);
            }

            currentEntrantCount = state.Count;

            checked
            {
                state.Count++;
            }
            return new(state);
        }


        [StructLayout(LayoutKind.Auto)]
        public readonly struct SingleThreadDisposable : IDisposable
        {
            readonly SingleThreadState state;
            internal SingleThreadDisposable(SingleThreadState state) => this.state = state;
            public void Dispose() => --state.Count;
        }


        /*  hashing function for token generation  ================================================================ */

        static int ComputeHash(ReadOnlySpan<char> text, int salt)
        {
            int hash = ComputeDjb2Hash(text);

            unchecked
            {
                hash = ((hash << 5) + hash) ^ salt;
            }

            return hash;
        }

        // non-cryptographic hash
        static int ComputeDjb2Hash(ReadOnlySpan<char> content)
        {
            int hash = 5381;
            int length = content.Length;
            int offset = 0;

            while (length >= 8)
            {
                unchecked
                {
                    hash = ((hash << 5) + hash) ^ content[offset + 0];
                    hash = ((hash << 5) + hash) ^ content[offset + 1];
                    hash = ((hash << 5) + hash) ^ content[offset + 2];
                    hash = ((hash << 5) + hash) ^ content[offset + 3];
                    hash = ((hash << 5) + hash) ^ content[offset + 4];
                    hash = ((hash << 5) + hash) ^ content[offset + 5];
                    hash = ((hash << 5) + hash) ^ content[offset + 6];
                    hash = ((hash << 5) + hash) ^ content[offset + 7];
                }
                length -= 8;
                offset += 8;
            }

            if (length >= 4)
            {
                unchecked
                {
                    hash = ((hash << 5) + hash) ^ content[offset + 0];
                    hash = ((hash << 5) + hash) ^ content[offset + 1];
                    hash = ((hash << 5) + hash) ^ content[offset + 2];
                    hash = ((hash << 5) + hash) ^ content[offset + 3];
                }
                length -= 4;
                offset += 4;
            }

            while (length > 0)
            {
                unchecked
                {
                    hash = ((hash << 5) + hash) ^ content[offset];
                }
                length -= 1;
                offset += 1;
            }

            return hash;
        }

    }
}




#region ////////  TEMPLATE: Unity Editor Tests  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.TEST.The_Sentinel  // must be unique. don't reuse existing namespace
{
    static class UNITY_EDITOR_TESTS  // don't change
    {
        const string MENU_ROOT = nameof(TEST) + "/" + nameof(The_Sentinel) + "/";


        #region ////////  TEMPLATE: Test Methods  ////////
        /*  TEMPLATE: Test Methods  ================================================================ */

        static int[] THE_VALUE = new int[13];
        static int[] NUM_INVOCATION = new int[13];
        volatile static int[] ALREADY_RAN = new int[13];

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Enter_Exclusive_Scope), priority = 0)]
        [Test]
        static void Enter_Exclusive_Scope()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            RunDebounceTest(1);


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Enter_Debouce_Scope_3), priority = 0)]
        [Test]
        static void Enter_Debouce_Scope_3()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            RunDebounceTest(3);


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Enter_Debouce_Scope_12), priority = 0)]
        [Test]
        static void Enter_Debouce_Scope_12()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            RunDebounceTest(12);


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        static void RunDebounceTest(int count)
        {
            if (Interlocked.Exchange(ref ALREADY_RAN[count], 1) != 0)
            {
                UnityEngine.Debug.Log("Test has already ran: debounce -> " + count);
                return;
            }

            UnityEngine.Debug.Log($"Threads starting... 2 log messages expected: debounce -> " + count);

            THE_VALUE[count] = 0;
            NUM_INVOCATION[count] = 0;

            int wait_trial = 2000;
            int wait_exclusive = 3000;

            SentinelToken st = Sentinel.GetUniqueToken();
            var startingSignal = new ManualResetEventSlim(false, 0);

            var ct = new CancellationTokenSource(wait_trial).Token;

            for (int i = 0; i < count * 2; i++)
            {
                // first one will be main thread
                Task.Run(async () =>
                {
                    try
                    {
                        startingSignal.Wait();
                        using (Sentinel.DebounceScope(st, count, out var rejected))
                        {
                            if (rejected)
                                goto SUB_WORKER;

                            var val = ++THE_VALUE[count];
                            if (val > count)
                            {
                                UnityEngine.Debug.LogError("other thread was entered into exclusive block before main thread...!!");
                            }

                            await Task.Delay(wait_exclusive).ConfigureAwait(false);
                        }

                        UnityEngine.Debug.Log($"debounce -> {count} \t tried to enter debounce block (roughly): {NUM_INVOCATION[count]:#,0}");

                        Assert.That(THE_VALUE[count], Is.EqualTo(1));

                        // try enter once again
                        using (Sentinel.DebounceScope(st, count, out var rejected))
                        {
                            if (rejected)
                                return;

                            THE_VALUE[count]++;
                        }

                        Assert.That(THE_VALUE[count], Is.EqualTo(2));

                        return;


                    SUB_WORKER:
                        do
                        {
                            using (Sentinel.DebounceScope(st, count, out var rejected))
                            {
                                if (rejected)
                                    goto GO_NEXT;

                                THE_VALUE[count]++;
                            }

                        GO_NEXT:
                            NUM_INVOCATION[count]++;
                        }
                        while (!ct.IsCancellationRequested);

                        UnityEngine.Debug.Log("sub worker finished: debounce -> " + count);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref ALREADY_RAN[count], 0);
                    }
                });
            }


            startingSignal.Set();
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
