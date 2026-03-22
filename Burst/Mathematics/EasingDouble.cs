// High Performance Easing Functions for .NET / Unity
// (c) 2026 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

// Unlike to Unity's `Mathf`, `Math` doesn't have `LerpUnclamped` method.
// --> Workaround: double value = from + ((to - from) * t);

// Checklist:
// - No unnecessary division ops?
// - No float number literals?
// - No float, double, int, long and var?
// - To find non floating point double literals
//   --> (?<!\.)\b\d+\b(?!\.)



// PRECISION SETTINGS

///#define __burst_float

using PRECISION = System.Double;
using INTEGER = System.Int64;
using SYSMATH = System.Math;



using System;
using System.Runtime.CompilerServices;

#if STMG_BURST_EXISTS
using Unity.Burst;
#endif

#nullable enable
#pragma warning disable IDE0065  // using directive placement
#pragma warning disable IDE0001  // Simplify name

namespace SatorImaging.UnityFundamentals
{
#if STMG_UNITYMATH_EXISTS
    using Math = Unity.Mathematics.math;
    internal static partial class UnityMathPolyfills { internal static void ToKeepUsingStatement(PRECISION _) => SYSMATH.Abs(_); }
#else
#pragma warning disable IDE1006  // Naming Styles
    using Math = SatorImaging.UnityFundamentals.UnityMathPolyfills;
    internal static partial class UnityMathPolyfills
    {
        public const PRECISION PI = SYSMATH.PI;

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static PRECISION exp(PRECISION x) => SYSMATH.Exp(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static PRECISION sin(PRECISION x) => SYSMATH.Sin(x);
    }
#pragma warning restore IDE1006
#endif

#if STMG_BURST_EXISTS
#if __burst_float
    [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
#else
    [BurstCompile(FloatPrecision = FloatPrecision.High, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
#endif
#endif
    public struct EasingDouble
    {
        /*  Quad  ================================================================ */

        /// <summary><c>=> pow(x, 2)</c></summary>
        public static PRECISION QuadIn(PRECISION x) => x * x;
        public static PRECISION QuadOut(PRECISION x)
        {
            PRECISION t = 1.0 - x;
            return 1.0 - (t * t);
        }
        public static PRECISION QuadInOut(PRECISION x)
        {
            if (x < 0.5)
            {
                return 2.0 * x * x;
            }

            PRECISION t = (-2.0 * x) + 2.0;
            return 1.0 - (t * t * 0.5);
        }


        /*  Cubic  ================================================================ */

        /// <summary><c>=> pow(x, 3)</c></summary>
        public static PRECISION CubicIn(PRECISION x) => x * x * x;
        public static PRECISION CubicOut(PRECISION x)
        {
            PRECISION t = 1.0 - x;
            return 1.0 - (t * t * t);
        }
        public static PRECISION CubicInOut(PRECISION x)
        {
            if (x < 0.5)
            {
                return 4.0 * x * x * x;
            }

            PRECISION t = (-2.0 * x) + 2.0;
            return 1.0 - (t * t * t * 0.5);
        }


        /*  Quart  ================================================================ */

        /// <summary><c>=> pow(x, 4)</c></summary>
        public static PRECISION QuartIn(PRECISION x) => x * x * x * x;
        public static PRECISION QuartOut(PRECISION x)
        {
            PRECISION t = 1.0 - x;
            return 1.0 - (t * t * t * t);
        }
        public static PRECISION QuartInOut(PRECISION x)
        {
            if (x < 0.5)
            {
                return 8.0 * x * x * x * x;
            }

            PRECISION t = (-2.0 * x) + 2.0;
            PRECISION t2 = t * t;
            return 1.0 - (t2 * t2 * 0.5);
        }


        /*  Quint  ================================================================ */

        /// <summary><c>=> pow(x, 5)</c></summary>
        public static PRECISION QuintIn(PRECISION x) => x * x * x * x * x;
        public static PRECISION QuintOut(PRECISION x)
        {
            PRECISION t = 1.0 - x;
            return 1.0 - (t * t * t * t * t);
        }
        public static PRECISION QuintInOut(PRECISION x)
        {
            if (x < 0.5)
            {
                return 16.0 * x * x * x * x * x;
            }

            PRECISION t = (-2.0 * x) + 2.0;
            PRECISION t2 = t * t;
            return 1.0 - (t2 * t2 * t * 0.5);
        }


        /*  Sine  ================================================================ */

        // x5 faster but low quality especially for Elastic (in slo mo, less motion continuity)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION Sin5(PRECISION t)
        {
            const PRECISION inv6 = 1.0 / 6.0;
            const PRECISION inv120 = 1.0 / 120.0;

            // sin(t) ≈ t - t^3/6 + t^5/120
            PRECISION t2 = t * t;
            PRECISION t3 = t2 * t;
            PRECISION t5 = t3 * t2;
            return t - (t3 * inv6) + (t5 * inv120);
        }

        /// <summary>Uses approximate sine value.</summary>
        public static PRECISION SineIn(PRECISION x) => 1.0 - Sin5((1.0 - x) * Math.PI * 0.5);
        public static PRECISION SineOut(PRECISION x) => Sin5(x * Math.PI * 0.5);
        public static PRECISION SineInOut(PRECISION x) => (1.0 - Sin5((0.5 - x) * Math.PI)) * 0.5;


        /*  Expo  ================================================================ */

        private const PRECISION ln_2 = 0.6931471805599453;

        /// <summary><c>=> pow(2.0, 10.0 * x - 10.0)</c></summary>
        public static PRECISION ExpoIn(PRECISION x) => x == 0.0 ? 0.0 : Math.exp(((10.0 * x) - 10.0) * ln_2);
        public static PRECISION ExpoOut(PRECISION x) => x == 1.0 ? 1.0 : 1.0 - Math.exp((-10.0 * x) * ln_2);
        public static PRECISION ExpoInOut(PRECISION x)
        {
            if (x == 0.0)
            {
                return 0.0;
            }
            if (x == 1.0)
            {
                return 1.0;
            }

            PRECISION t = (20.0 * x) - 10.0;
            return x < 0.5
                ? Math.exp(t * ln_2) * 0.5
                : (2.0 - Math.exp((-t) * ln_2)) * 0.5;
        }


        /*  Circ  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION FastSqrt(PRECISION x)
        {
            if (x <= 0.0)
            {
                return 0.0;
            }

            PRECISION xHalf = x * 0.5;
            INTEGER i = BitConverter.DoubleToInt64Bits(x);
            i = 0x5fe6eb50c7b537a9L - (i >> 1);

            PRECISION y = BitConverter.Int64BitsToDouble(i);
            y *= 1.5 - (xHalf * y * y);
            return x * y;
        }

        /// <summary><c>=> 1.0 - sqrt(1.0 - x * x)</c></summary>
        public static PRECISION CircIn(PRECISION x) => 1.0 - FastSqrt(1.0 - (x * x));
        public static PRECISION CircOut(PRECISION x)
        {
            PRECISION t = x - 1.0;
            return FastSqrt(1.0 - (t * t));
        }
        public static PRECISION CircInOut(PRECISION x)
        {
            PRECISION t = 2.0 * x;
            if (x < 0.5)
            {
                return (1.0 - FastSqrt(1.0 - (t * t))) * 0.5;
            }

            PRECISION u = 2.0 - t;
            return (FastSqrt(1.0 - (u * u)) + 1.0) * 0.5;
        }


        /*  Back  ================================================================ */

        private const PRECISION c1 = 1.70158;
        private const PRECISION c2 = c1 * 1.525;

        /// <summary>Overshoots the range once and returns.</summary>
        public static PRECISION BackIn(PRECISION x) => ((c1 + 1.0) * x * x * x) - (c1 * x * x);
        public static PRECISION BackOut(PRECISION x)
        {
            PRECISION t = x - 1.0;
            return 1.0 + ((c1 + 1.0) * t * t * t) + (c1 * t * t);
        }
        public static PRECISION BackInOut(PRECISION x)
        {
            PRECISION t = 2.0 * x;
            if (x < 0.5)
            {
                return (t * t * (((c2 + 1.0) * t) - c2)) * 0.5;
            }

            PRECISION u = t - 2.0;
            return ((u * u * (((c2 + 1.0) * u) + c2)) + 2.0) * 0.5;
        }


        /*  Elastic  ================================================================ */

        private const PRECISION e_c4 = 2.0 * Math.PI / 3.0;
        private const PRECISION e_c5 = 2.0 * Math.PI / 4.5;

        /// <summary>Simulates spring-like oscillations.</summary>
        public static PRECISION ElasticIn(PRECISION x)
        {
            if (x == 0.0)
            {
                return 0.0;
            }

            if (x == 1.0)
            {
                return 1.0;
            }

            PRECISION t = 10.0 * x;
            return -Math.exp((t - 10.0) * ln_2) * Math.sin((t - 10.75) * e_c4);
        }

        public static PRECISION ElasticOut(PRECISION x)
        {
            if (x == 0.0)
            {
                return 0.0;
            }

            if (x == 1.0)
            {
                return 1.0;
            }

            PRECISION t = 10.0 * x;
            return (Math.exp((-t) * ln_2) * Math.sin((t - 0.75) * e_c4)) + 1.0;
        }

        public static PRECISION ElasticInOut(PRECISION x)
        {
            if (x == 0.0)
            {
                return 0.0;
            }

            if (x == 1.0)
            {
                return 1.0;
            }

            PRECISION t = (20.0 * x) - 10.0;
            PRECISION angle = (t - 1.125) * e_c5;
            PRECISION s = Math.sin(angle) * 0.5;

            if (x < 0.5)
            {
                return -Math.exp(t * ln_2) * s;
            }

            return (Math.exp(-t * ln_2) * s) + 1.0;
        }


        /*  Bounce  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION BounceOut_Impl(PRECISION x)
        {
            const PRECISION n1 = 7.5625;
            const PRECISION d1_inv = 1.0 / 2.75;
            if (x < 1.0 * d1_inv)
            {
                return n1 * x * x;
            }
            else if (x < 2.0 * d1_inv)
            {
                x -= 1.5 * d1_inv;
                return (n1 * x * x) + 0.75;
            }
            else if (x < 2.5 * d1_inv)
            {
                x -= 2.25 * d1_inv;
                return (n1 * x * x) + 0.9375;
            }
            else
            {
                x -= 2.625 * d1_inv;
                return (n1 * x * x) + 0.984375;
            }
        }

        /// <summary>Simulates a bouncing motion against a boundary.</summary>
        public static PRECISION BounceIn(PRECISION x) => 1.0 - BounceOut_Impl(1.0 - x);
        public static PRECISION BounceOut(PRECISION x) => BounceOut_Impl(x);
        public static PRECISION BounceInOut(PRECISION x)
        {
            PRECISION t = 2.0 * x;
            return x < 0.5 ? (1.0 - BounceOut_Impl(1.0 - t)) * 0.5 : (1.0 + BounceOut_Impl(t - 1.0)) * 0.5;
        }
    }
}
