/** Early Finally Functions for .NET / Unity
 ** (c) 2024 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

How to Use
==========
```cs
// try-finally pattern made simple
using var rental = ArrayPool<byte>.Shared.Rent(256)
    .Defer(static x =>
    {
        // this will be executed when exiting 'rental' variable scope
        ArrayPool<byte>.Shared.Return(x, clearArray: true);
    });

// 'Value' to access extension method receiver
var span = rental.Value.AsSpan();

// create Defer action from scratch
using var _ = Defer.New(something.Value, (restore) => something.Value = restore);
something.Value = tempValue;

// 'using-block' example
var data = (name: "value tuple", value: 3.10f);
using (data.Defer(static x => Console.WriteLine($"Disposed: {x.name} ({x.value}")))
{
    Console.WriteLine("Disposing " + data.Value.name);
}

// note that 'data' still be accessible after disposed
// block-less-using is recommended to make variable scope sync-ed
data.value = -310;
```

 */

using NUnit.Framework;
using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.InteropServices;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    public static class Defer
    {
        readonly static Action<Action> OnDispose = (act) => act.Invoke();
        public static DeferredDisposable<Action> New(Action act) => new(act, OnDispose);

        public static DeferredDisposable<T> New<T>(T state, Action<T> act) => new(state, act);


        /// <summary>
        /// Designed to being used with <see langword="using"/> statement.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct DeferredDisposable<T> : IDisposable
        {
            private readonly Action<T> onDispose;
            public readonly T Value;

            public DeferredDisposable(T Value, Action<T> onDispose)
            {
                this.Value = Value;
                this.onDispose = onDispose;
            }

            [EditorBrowsable(EditorBrowsableState.Never)]
            readonly public void Dispose() => onDispose.Invoke(Value);
        }

        /// <summary>
        /// Designed to being used with <see langword="using"/> statement.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct DeferredDisposable<T, TState> : IDisposable
        {
            private readonly Action<T, TState> onDispose;
            public readonly T Value;
            public readonly TState State;

            public DeferredDisposable(T Value, Action<T, TState> onDispose, TState State)
            {
                this.Value = Value;
                this.onDispose = onDispose;
                this.State = State;
            }

            [EditorBrowsable(EditorBrowsableState.Never)]
            readonly public void Dispose() => onDispose.Invoke(Value, State);
        }

    }


    public static class DeferExtensions
    {
        /// <summary>
        /// Always execute action when exiting method or block scope even if fault by exception.
        /// </summary>
        /// <remarks>
        /// DO declare lambda as <see langword="static"/> if possible. It will reuse existing instance. ex: <c>.Defer(static x => {...});</c>
        /// <br/>
        /// DON'T pass static method directly. C# 9.0 will allocate <c>Action&lt;T></c> on every call. ex: <c>.Defer(MyStaticMethod);</c>
        /// </remarks>
        /// <returns>
        /// Disposable <see langword="struct"/> for <see langword="using"/> statement.
        /// </returns>
        public static Defer.DeferredDisposable<T> Defer<T>(this T self, Action<T> act)
            where T : class
        {
            return new(self, act);
        }

        /// <inheritdoc cref="Defer{T}(T, Action{T})"/>
        [Obsolete("Value type is copied into `DeferredAction<T>` instance and callback is invoked with it. (ie. could have old value) Consider using `this.Defer(x => x._myValueTypeAsField)` instead to take updated value in callback.")]
        public static Defer.DeferredDisposable<T> Defer<T>(this ref T self, Action<T> act)
            where T : struct
        {
            return new(self, act);
        }


        /// <inheritdoc cref="Defer{T}(T, Action{T})"/>
        public static Defer.DeferredDisposable<T, TState> Defer<T, TState>(this T self, TState state, Action<T, TState> act)
            where T : class
        {
            return new(self, act, state);
        }

        /// <inheritdoc cref="Defer{T}(T, Action{T})"/>
        [Obsolete("Value type is copied into `DeferredAction<T>` instance and callback is invoked with it. (ie. could have old value) Consider using `this.Defer(x => x._myValueTypeAsField)` instead to take updated value in callback.")]
        public static Defer.DeferredDisposable<T, TState> Defer<T, TState>(this ref T self, TState state, Action<T, TState> act)
            where T : struct
        {
            return new(self, act, state);
        }

    }
}




#region ////////  TEMPLATE: Unity Editor Tests  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace SatorImaging.UnityFundamentals.TEST.Defer_Tests  // must be unique. don't reuse existing namespace
{
    static class UNITY_EDITOR_TESTS  // don't change
    {
        const string MENU_ROOT = nameof(TEST) + "/" + nameof(Defer_Tests) + "/";


        #region ////////  TEMPLATE: Test Methods  ////////
        /*  TEMPLATE: Test Methods  ================================================================ */

        // TEMPLATE: copy & paste and replace argument for 'nameof()'
        [UnityEditor.MenuItem(MENU_ROOT + nameof(Basic_Tests), priority = 0)]
        [Test]
        public static void Basic_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used

            using var secret = ArrayPool<byte>.Shared.Rent(77).Defer(static x =>
            {
                ArrayPool<byte>.Shared.Return(x, clearArray: true);
                UnityEngine.Debug.Log($"Disposed: rental array (length:{x.Length})");
            });

            UnityEngine.Debug.Log("Rental array length: " + secret.Value.Length);


            using (var anony = new { name = "anonymous", value = 3.10f }.Defer(static x => UnityEngine.Debug.Log($"Disposed: {x.name} ({x.value})")))
            {
                UnityEngine.Debug.Log("Disposing " + anony.Value.name);
            }

            using (var tuple = new Tuple<string, long>("tuple class", -310).Defer(static x => UnityEngine.Debug.Log($"Disposed: {x.Item1} ({x.Item2})")))
            {
                UnityEngine.Debug.Log("Disposing " + tuple.Value.Item1);
            }

            using (var record = new MyRecord("record class", 310_000).Defer(static x => UnityEngine.Debug.Log($"Disposed: {x.Name} ({x.Value})")))
            {
                record.Value.PrintValue();
            }

            var data = (name: "value tuple", value: 310);
            using (data.Defer(static x => UnityEngine.Debug.Log($"Disposed: {x.name} ({x.value})")))
            {
                UnityEngine.Debug.Log("Disposing " + data.name);
            }


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }


        public record MyRecord(string Name, int Value)
        {
            internal string Name { get; } = Name;
            public int Value { get; private set; } = Value;

            public void PrintValue() => UnityEngine.Debug.Log(nameof(MyRecord) + $" method: {Name} ({Value:#,0})");

            public virtual bool Equals(MyRecord? other) => other is not null && ReferenceEquals(this, other);
            public override int GetHashCode() => HashCode.Combine(typeof(MyRecord), Name, Value);
        }


        /*  TEMPLATE: End of Tests  ================================================================ */
        #endregion    //  TEMPLATE: End of Tests


        /* TEMPLATE: add 'using NUnit.Framework;' to header of script to fix error */

        /* TEMPLATE: copy & paste and replace argument for 'nameof()'

        [UnityEditor.MenuItem(MENU_ROOT + nameof(__Underscore_Separated_Method_Name__), priority = 0)]
        [Test]
        public static void Basic_Tests()
        {
            // TEST: write test code here
            //       > 'Assert.That(..., Is/Throws)' can be used


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
        }

        */


        // TEMPLATE: run all tests in this class
        [UnityEditor.MenuItem(MENU_ROOT + "Run All Tests", priority = int.MinValue + 310)]
        public static void UnityEditorTests_RunAllTests()
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
        public static void UnityEditorTests_EditTests() => __EditTests();

        static void __EditTests(
            [System.Runtime.CompilerServices.CallerFilePath] string? filePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
            => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
    }
}
#endif
#endregion
