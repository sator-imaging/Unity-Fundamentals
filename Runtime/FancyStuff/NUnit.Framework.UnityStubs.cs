/** NUnit-compatible Framework for Unity Editor
 ** (c) 2024 Sator Imaging, Licensed under the MIT License
 ** https://github.com/sator-imaging/Unity-Fundamentals

Drop-in replacement for NUnit designed to use in asset which released on Unity AssetStore. As different from
UPM packages, generic asset has no way to define dependency on `com.unity.ext.nunit` by package.json file.

 */

/*  uncomment to debug  */
//#undef STMG_TEST_NUNIT_EXISTS

#if STMG_TEST_NUNIT_EXISTS == false
#define STMG_TEST_ENABLE_STUBS
#endif

using System;

#nullable enable

#if STMG_TEST_ENABLE_STUBS

#pragma warning disable IDE1006

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class TestAttribute : Attribute
    {
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Serializable]
    internal class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
        protected AssertionException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }


    /*  Assert.That  ================================================================ */

    internal static class Assert
    {
        // NOTE: unity "excludes" assembly from build which has this preprocessor symbol
        //       in editor, this symbol is defined always, all assemblies.
        const string UNITY_TESTS = "UNITY_INCLUDE_TESTS";


        /*  helper  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Success(string msg, string? testExpression, string? memberName, string? filePath, int lineNumber)
        {
            if (UNITY_EDITOR.Get_VerboseLogging())
            {
                if (testExpression != null)
                    msg += $" \t code: `{testExpression}`";
                UnityEngine.Debug.Log($"[OK] {memberName}: {msg}\n\n{filePath} (line:{lineNumber})\n");
            }
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        static AssertionException Assertion(string unexpectedConditionDescription)
        {
            return new(unexpectedConditionDescription);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static AssertionException Assertion(bool condition, string? msg)
        {
            var formatted = "condition doesn't met";
            if (msg != null)
                formatted += ": " + msg;

            return new(formatted);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static AssertionException Assertion<T>(T value, IThat<T> constraint, string? msg)
        {
            var formatted = string.Format(constraint.Message, value, constraint.Expected);
            if (msg != null)
                formatted += ": " + msg;

            return new(formatted);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static AssertionException Assertion<T>(Exception value, IThrows<T> constraint, string? msg)
        {
            var formatted = string.Format(constraint.Message, value.GetType().Name + ": " + value.Message, typeof(T).Name);
            if (msg != null)
                formatted += ": " + msg;

            return new(formatted);
        }


        /*  Is  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional(UNITY_TESTS)]
        public static void That(bool condition,
            // TODO: C# 10.0 required --> [CallerArgumentExpressionAttribute]
            string? testExpression = null,
            [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
            if (condition)
            {
                Success($"'{true}'", testExpression, memberName, filePath, lineNumber);
                return;
            }

            throw Assertion(condition, testExpression);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional(UNITY_TESTS)]
        public static void That<T>(T value, IIs<T> constraint,
            // TODO: C# 10.0 required --> [CallerArgumentExpressionAttribute]
            string? testExpression = null,
            [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
            if (constraint.IsTrue(value))
            {
                Success($"'{value}'  ({value?.GetType().Name ?? "<UNKNOWN TYPE>"})", testExpression, memberName, filePath, lineNumber);
                return;
            }

            throw Assertion(value, constraint, testExpression);
        }


        /*  AreEqual  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional(UNITY_TESTS)]
        public static void AreEqual<T>(T value, T other,
            // TODO: C# 10.0 required --> [CallerArgumentExpressionAttribute]
            string? testExpression = null,
            [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
            => That(value, Is.EqualTo(other), testExpression, memberName, filePath, lineNumber);


        /*  Throws  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional(UNITY_TESTS)]
        public static void That<T>(Func<object> expr, IThrows<T> constraint,
            // TODO: C# 10.0 required --> [CallerArgumentExpressionAttribute]
            string? testExpression = null,
            [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
            where T : Exception
            => That((Action)(() => expr()), constraint, testExpression, memberName, filePath, lineNumber);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional(UNITY_TESTS)]
        public static void That<T>(Action expr, IThrows<T> constraint,
            // TODO: C# 10.0 required --> [CallerArgumentExpressionAttribute]
            string? testExpression = null,
            [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
            where T : Exception
        {
            if (typeof(T) == typeof(Exception))
            {
                var warn = "<b>[WARN]</b> should not catch " + typeof(Exception);
                testExpression = (testExpression == null)
                    ? warn
                    : warn + "  " + testExpression;
            }

            // NOTE: the following code is required to make this method inlined in user test code
            var exc = ThrowIfFailed_NoInlining(expr, constraint, testExpression, memberName, filePath, lineNumber);
            if (exc != null)
                throw exc;
        }


        // NOTE: the following code is required to make this method inlined in user test code
        [MethodImpl(MethodImplOptions.NoInlining)]
        static AssertionException? ThrowIfFailed_NoInlining<T>(Action expr, IThrows<T> constraint,
            // TODO: C# 10.0 required --> [CallerArgumentExpressionAttribute]
            string? testExpression = null,
            [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
        {
            try
            {
                expr.Invoke();
            }
            catch (Exception exc)
            {
                if (constraint.IsTrue(exc))
                {
                    Success($"[{exc.GetType().Name}] {exc.Message}", testExpression, memberName, filePath, lineNumber);
                    return null;
                }
                else
                {
                    return Assertion(exc, constraint, testExpression);
                }
            }

            return Assertion("expected error doesn't occur: " + constraint.Type.Name);
        }


#if UNITY_EDITOR
        public static class UNITY_EDITOR
        {
            const int PRIORITY = int.MaxValue - 310;
            const string MENU_RUN_ALL = "TEST/Run All Tests";
            const string MENU_VERBOSE = "TEST/Show Succeeded Test Logs";
            const string PREF_VERBOSE = nameof(SatorImaging) + nameof(SatorImaging.UnityFundamentals) + nameof(NUnit) + MENU_VERBOSE;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Get_VerboseLogging() => UnityEditor.EditorPrefs.GetBool(PREF_VERBOSE, false);

            [UnityEditor.MenuItem(MENU_VERBOSE, priority = PRIORITY, validate = true)]
            public static bool Validate_VerboseLogging()
            {
                UnityEditor.Menu.SetChecked(MENU_VERBOSE, Get_VerboseLogging());
                return true;
            }

            [UnityEditor.MenuItem(MENU_VERBOSE, priority = PRIORITY)]
            public static bool Toggle_VerboseLogging()
            {
                var toggled = !Get_VerboseLogging();
                UnityEditor.EditorPrefs.SetBool(PREF_VERBOSE, toggled);
                UnityEditor.Menu.SetChecked(MENU_VERBOSE, toggled);
                return toggled;
            }


            const int PRIORITY_RUN_ALL = PRIORITY + 31;

            [UnityEditor.MenuItem(MENU_RUN_ALL, priority = PRIORITY_RUN_ALL)]
            public static void Run_All_Tests() => Internal_RunAllTests(1);

            [UnityEditor.MenuItem(MENU_RUN_ALL + " x3", priority = PRIORITY_RUN_ALL)]
            public static void Run_All_Tests_x3() => Internal_RunAllTests(3);

            static void Internal_RunAllTests(int count)
            {
                var clsNameSet = new HashSet<string>();

                foreach (var method in UnityEditor.TypeCache.GetMethodsWithAttribute<TestAttribute>())
                {
                    for (int i = 1; i <= count; i++)
                    {
                        method.Invoke(null, null);
                    }

                    clsNameSet.Add(method.DeclaringType.Namespace);
                }

                UnityEngine.Debug.Log(@"  <b>\\\   All Tests Passed   ///</b>  click to see details..."
                    + "\n\n" + string.Join("\n", clsNameSet.Select((x, i) => (i + 1) + ")  " + x)) + "\n");
            }
        }
#endif
    }


    internal interface IThat<T>
    {
        /// <summary>format: 0 = actual, 1 = expected.</summary>
        string Message { get; }
        T Expected { get; }
        bool IsTrue(T value);
    }


    /*  Throws  ================================================================ */

    internal interface IThrows<T> : IThat<T>
    {
        bool IsTrue(Exception value);
        Type Type { get; }
    }

    internal sealed class Throws
    {
        public static __TypeOf<T> TypeOf<T>() => new();
        [EditorBrowsable(EditorBrowsableState.Never)]
        public record __TypeOf<T>(T Expected = default!, string Message = "'{1}' has not thrown: '{0}'")
            : IThrows<T>
        {
            public T Expected { get; private set; } = Expected;
            public string Message { get; private set; } = Message;
            public bool IsTrue(T value) => value is not null;

            public bool IsTrue(Exception value) => value?.GetType() == typeof(T);
            public Type Type => typeof(T);
        }
    }


    /*  Has  ================================================================ */

    internal interface IHas<T> : IThat<T>
    {
    }

    internal sealed class Has
    {
        // TODO: collection constraints
    }


    /*  Does  ================================================================ */

    internal interface IDoes<T> : IThat<T>
    {
    }

    internal sealed class Does
    {
        // TODO: https://docs.nunit.org/api/NUnit.Framework.Does.html#methods
    }


    /*  Is  ================================================================ */

    internal interface IIs<T> : IThat<T>
    {
    }

    internal sealed class Is
    {
        static AssertionException ERR<T>(string? msg = null) => new($"[FATAL] unsupported type: {typeof(T).Name} ({msg ?? "<NO MSG>"})");

        // TODO: InRange(0, 100, minExclusive: false, maxExclusive: false)
        // TODO: Positive(), Negative()

        //equalTo
        public static __EqualTo<T> EqualTo<T>(T value) => new(value);
        [EditorBrowsable(EditorBrowsableState.Never)]
        public record __EqualTo<T>(T Expected, string Message = "'{0}' is not equal to '{1}'")
            : IIs<T>
        {
            public T Expected { get; private set; } = Expected;
            public string Message { get; private set; } = Message;
            public bool IsTrue(T value) => (value as IEquatable<T>)?.Equals(Expected) ?? EqualityComparer<T>.Default.Equals(value, Expected);
        }

        //greaterThan
        public static __GreaterThan<T> GreaterThan<T>(T value) => new(value);
        [EditorBrowsable(EditorBrowsableState.Never)]
        public record __GreaterThan<T>(T Expected, string Message = "'{0}' is not greater than '{1}'")
            : IIs<T>
        {
            public T Expected { get; private set; } = Expected;
            public string Message { get; private set; } = Message;
            public bool IsTrue(T value) => ((value as IComparable<T>)?.CompareTo(Expected) ?? throw ERR<T>()) > 0;
        }

        public static __GreaterThanOrEqualTo<T> GreaterThanOrEqualTo<T>(T value) => new(value);
        [EditorBrowsable(EditorBrowsableState.Never)]
        public record __GreaterThanOrEqualTo<T>(T Expected, string Message = "'{0}' is not greater than or equal to '{1}'")
            : IIs<T>
        {
            public T Expected { get; private set; } = Expected;
            public string Message { get; private set; } = Message;
            public bool IsTrue(T value) => ((value as IComparable<T>)?.CompareTo(Expected) ?? throw ERR<T>()) >= 0;
        }

        //lessThan
        public static __LessThan<T> LessThan<T>(T value) => new(value);
        [EditorBrowsable(EditorBrowsableState.Never)]
        public record __LessThan<T>(T Expected, string Message = "'{0}' is not less than '{1}'")
            : IIs<T>
        {
            public T Expected { get; private set; } = Expected;
            public string Message { get; private set; } = Message;
            public bool IsTrue(T value) => ((value as IComparable<T>)?.CompareTo(Expected) ?? throw ERR<T>()) < 0;
        }

        public static __LessThanOrEqualTo<T> LessThanOrEqualTo<T>(T value) => new(value);
        [EditorBrowsable(EditorBrowsableState.Never)]
        public record __LessThanOrEqualTo<T>(T Expected, string Message = "'{0}' is not less than or equal to '{1}'")
            : IIs<T>
        {
            public T Expected { get; private set; } = Expected;
            public string Message { get; private set; } = Message;
            public bool IsTrue(T value) => ((value as IComparable<T>)?.CompareTo(Expected) ?? throw ERR<T>()) <= 0;
        }
    }
}
#endif




#region ////////  TEMPLATE: Unity Editor Tests  ////////

#if UNITY_EDITOR

#pragma warning disable IDE1006

// TEMPLATE: namespace naming convention: <TEST_TARGET>.<ROOT_MENU_LABEL>.<SUB_MENU_LABEL>
namespace NUnit.Framework.TEST.NUnit_Unity_Stubs  // must be unique. don't reuse existing namespace
{
    static class UNITY_EDITOR_TESTS  // don't change
    {
        const string MENU_ROOT = nameof(TEST) + "/" + nameof(NUnit_Unity_Stubs) + "/";


        #region ////////  TEMPLATE: Test Methods  ////////
        /*  TEMPLATE: Test Methods  ================================================================ */

        [UnityEditor.MenuItem(MENU_ROOT + nameof(Basic_Tests), priority = 0)]
        [Test]
        public static void Basic_Tests()
        {
            // TEST: write test code here
            //       > `Assert.That(..., Is/Throws)` can be used

            //bool
            Assert.That(true);
            Assert.That(() =>
                Assert.That(false),
                Throws.TypeOf<AssertionException>());

            //equalTo
            Assert.That(0, Is.EqualTo(0));
            Assert.That(() =>
                Assert.That(int.MinValue, Is.EqualTo(0)),
                Throws.TypeOf<AssertionException>());
            Assert.That(() =>
                Assert.That(int.MaxValue, Is.EqualTo(0)),
                Throws.TypeOf<AssertionException>());

            //greaterThan
            Assert.That(int.MaxValue, Is.GreaterThan(0));
            Assert.That(() =>
                Assert.That(0, Is.GreaterThan(0)),
                Throws.TypeOf<AssertionException>());

            //lessThan
            Assert.That(int.MinValue, Is.LessThan(0));
            Assert.That(() =>
                Assert.That(0, Is.LessThan(0)),
                Throws.TypeOf<AssertionException>());

            //greaterThanOrEqualTo
            Assert.That(0, Is.GreaterThanOrEqualTo(0));
            Assert.That(() =>
                Assert.That(int.MinValue, Is.GreaterThanOrEqualTo(0)),
                Throws.TypeOf<AssertionException>());

            //lessThanOrEqualTo
            Assert.That(0, Is.LessThanOrEqualTo(0));
            Assert.That(() =>
                Assert.That(int.MaxValue, Is.LessThanOrEqualTo(0)),
                Throws.TypeOf<AssertionException>());

            // expected exception hasn't thrown
            Assert.That(() =>
                Assert.That(() => throw new NullReferenceException(), Throws.TypeOf<Exception>()),
                Throws.TypeOf<AssertionException>());

            Assert.That(() =>
                Assert.That(() => { _ = "TEST"; }, Throws.TypeOf<Exception>()),
                Throws.TypeOf<AssertionException>());

            Assert.That(() =>
                Assert.That(() => true, Throws.TypeOf<Exception>()),
                Throws.TypeOf<AssertionException>());

            Assert.That(() =>
                Assert.That(() => (true, false), Throws.TypeOf<Exception>()),
                Throws.TypeOf<AssertionException>());

            // expected exception
            Assert.That(() => throw new NullReferenceException(), Throws.TypeOf<NullReferenceException>());


            // TEST: done!!
            UnityEngine.Debug.Log("[TEST] Completed Successfully!!");
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
