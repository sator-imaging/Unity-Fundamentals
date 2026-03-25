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



/// DEBUG
//#undef STMG_UNITYMATH_EXISTS
//#undef STMG_BURST_EXISTS


/// PRECISION SETTINGS
//#define __burst_float

using PRECISION = System.Double;
using INTEGER = System.Int64;
using SYSMATH = System.Math;



#if STMG_BURST_EXISTS
using Unity.Burst;
#endif

using System;
using System.Runtime.CompilerServices;

#nullable enable
#pragma warning disable IDE0065  // using directive placement
#pragma warning disable IDE0001  // Simplify name

namespace SatorImaging.UnityFundamentals
{
#if STMG_UNITYMATH_EXISTS
    using Math = Unity.Mathematics.math;
#else
#pragma warning disable IDE1006  // Naming Styles
    using Math = SatorImaging.UnityFundamentals.UnityMathPolyfills;
    internal static partial class UnityMathPolyfills
    {
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
        public static PRECISION QuadOut(PRECISION x) => 1.0 - ((1.0 - x) * (1.0 - x));
        public static PRECISION QuadInOut(PRECISION x) => x < 0.5 ? 2.0 * x * x : 1.0 - (((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * 0.5);


        /*  Cubic  ================================================================ */

        /// <summary><c>=> pow(x, 3)</c></summary>
        public static PRECISION CubicIn(PRECISION x) => x * x * x;
        public static PRECISION CubicOut(PRECISION x) => 1.0 - ((1.0 - x) * (1.0 - x) * (1.0 - x));
        public static PRECISION CubicInOut(PRECISION x) => x < 0.5 ? 4.0 * x * x * x : 1.0 - (((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * 0.5);


        /*  Quart  ================================================================ */

        /// <summary><c>=> pow(x, 4)</c></summary>
        public static PRECISION QuartIn(PRECISION x) => x * x * x * x;
        public static PRECISION QuartOut(PRECISION x) => 1.0 - ((1.0 - x) * (1.0 - x) * (1.0 - x) * (1.0 - x));
        public static PRECISION QuartInOut(PRECISION x) => x < 0.5 ? 8.0 * x * x * x * x : 1.0 - (((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * 0.5);


        /*  Quint  ================================================================ */

        /// <summary><c>=> pow(x, 5)</c></summary>
        public static PRECISION QuintIn(PRECISION x) => x * x * x * x * x;
        public static PRECISION QuintOut(PRECISION x) => 1.0 - ((1.0 - x) * (1.0 - x) * (1.0 - x) * (1.0 - x) * (1.0 - x));
        public static PRECISION QuintInOut(PRECISION x) => x < 0.5 ? 16.0 * x * x * x * x * x : 1.0 - (((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * 0.5);


        /*  Sine  ================================================================ */

        // x5 faster but low quality especially for Elastic (in slo mo, less motion continuity)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION Sin5(PRECISION t)
        {
            const PRECISION inv6 = 1.0 / 6.0;

            // Original formula: sin(t) ≈ t - t^3/6 + t^5/120
            // Targeted formula: sin(t) ≈ t - t^3/6 + t^5/C
            // Constant C is adjusted to fit 0..1 range as possible (targeted sin(PI/2) = 1).
            const PRECISION invC = 0.007860176264317486;

            return t - (t * t * t * inv6) + (t * t * t * t * t * invC);
        }

        /// <summary>Uses approximate sine value.</summary>
        public static PRECISION SineIn(PRECISION x) => x <= 0.0 ? 0.0 : (x >= 1.0 ? 1.0 : 1.0 - Sin5((1.0 - x) * SYSMATH.PI * 0.5));
        public static PRECISION SineOut(PRECISION x) => x <= 0.0 ? 0.0 : (x >= 1.0 ? 1.0 : Sin5(x * SYSMATH.PI * 0.5));
        public static PRECISION SineInOut(PRECISION x) => x <= 0.0 ? 0.0 : (x >= 1.0 ? 1.0 : (1.0 - Sin5((0.5 - x) * SYSMATH.PI)) * 0.5);


        /*  Expo  ================================================================ */

        private const PRECISION ln_2 = 0.6931471805599453;

        /// <summary><c>=> pow(2.0, 10.0 * x - 10.0)</c></summary>
        public static PRECISION ExpoIn(PRECISION x) => x == 0.0 ? 0.0 : Math.exp(((10.0 * x) - 10.0) * ln_2);
        public static PRECISION ExpoOut(PRECISION x) => x == 1.0 ? 1.0 : 1.0 - Math.exp((-10.0 * x) * ln_2);
        public static PRECISION ExpoInOut(PRECISION x) => x == 0.0 ? 0.0 : (x == 1.0 ? 1.0 : (x < 0.5 ? Math.exp(((20.0 * x) - 10.0) * ln_2) * 0.5 : (2.0 - Math.exp((10.0 - (20.0 * x)) * ln_2)) * 0.5));


        /*  Circ  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION FastSqrt(PRECISION x)
        {
            PRECISION xHalf = x * 0.5;
            INTEGER i = BitConverter.DoubleToInt64Bits(x);
            i = 0x5fe6eb50c7b537a9L - (i >> 1);

            PRECISION y = BitConverter.Int64BitsToDouble(i);

            // Original formula: y = y * (1.5 - xHalf * y * y)
            // Targeted formula: y = y * (K - xHalf * y * y)
            // Constant K is adjusted to fit 0..1 range as possible (targeted sqrt(1) = 1).
            y *= 1.501750997142437871 - (xHalf * y * y);

            return x <= 0.0 ? 0.0 : x * y;
        }

        /// <summary><c>=> 1.0 - sqrt(1.0 - x * x)</c></summary>
        public static PRECISION CircIn(PRECISION x) => x <= 0.0 ? 0.0 : (x >= 1.0 ? 1.0 : 1.0 - FastSqrt(1.0 - (x * x)));
        public static PRECISION CircOut(PRECISION x) => x <= 0.0 ? 0.0 : (x >= 1.0 ? 1.0 : FastSqrt(1.0 - ((x - 1.0) * (x - 1.0))));
        public static PRECISION CircInOut(PRECISION x) => x <= 0.0 ? 0.0 : (x >= 1.0 ? 1.0 : (x < 0.5 ? (1.0 - FastSqrt(1.0 - ((2.0 * x) * (2.0 * x)))) * 0.5 : (FastSqrt(1.0 - ((2.0 - (2.0 * x)) * (2.0 - (2.0 * x)))) + 1.0) * 0.5));


        /*  Back  ================================================================ */

        private const PRECISION c1 = 1.70158;
        private const PRECISION c2 = c1 * 1.525;

        /// <summary>Overshoots the range once and returns.</summary>
        public static PRECISION BackIn(PRECISION x) => ((c1 + 1.0) * x * x * x) - (c1 * x * x);
        public static PRECISION BackOut(PRECISION x) => 1.0 + ((c1 + 1.0) * (x - 1.0) * (x - 1.0) * (x - 1.0)) + (c1 * (x - 1.0) * (x - 1.0));
        public static PRECISION BackInOut(PRECISION x) => x < 0.5 ? ((2.0 * x) * (2.0 * x) * (((c2 + 1.0) * (2.0 * x)) - c2)) * 0.5 : (((2.0 * x - 2.0) * (2.0 * x - 2.0) * (((c2 + 1.0) * (2.0 * x - 2.0)) + c2)) + 2.0) * 0.5;


        /*  Elastic  ================================================================ */

        private const PRECISION e_c4 = 2.0 * SYSMATH.PI / 3.0;
        private const PRECISION e_c5 = 2.0 * SYSMATH.PI / 4.5;

        /// <summary>Simulates spring-like oscillations.</summary>
        public static PRECISION ElasticIn(PRECISION x) => x == 0.0 ? 0.0 : (x == 1.0 ? 1.0 : -Math.exp(((10.0 * x) - 10.0) * ln_2) * Math.sin(((10.0 * x) - 10.75) * e_c4));

        public static PRECISION ElasticOut(PRECISION x) => x == 0.0 ? 0.0 : (x == 1.0 ? 1.0 : (Math.exp((-10.0 * x) * ln_2) * Math.sin(((10.0 * x) - 0.75) * e_c4)) + 1.0);

        public static PRECISION ElasticInOut(PRECISION x) => x == 0.0 ? 0.0 : (x == 1.0 ? 1.0 : (x < 0.5 ? -Math.exp(((20.0 * x) - 10.0) * ln_2) * Math.sin(((20.0 * x) - 11.125) * e_c5) * 0.5 : (Math.exp((10.0 - (20.0 * x)) * ln_2) * Math.sin(((20.0 * x) - 11.125) * e_c5) * 0.5) + 1.0));


        /*  Bounce  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION BounceOut_Impl(PRECISION x)
        {
            const PRECISION n1 = 7.5625;
            const PRECISION d1_inv = 1.0 / 2.75;
            return x < 1.0 * d1_inv ? n1 * x * x :
                   x < 2.0 * d1_inv ? (n1 * (x - (1.5 * d1_inv)) * (x - (1.5 * d1_inv))) + 0.75 :
                   x < 2.5 * d1_inv ? (n1 * (x - (2.25 * d1_inv)) * (x - (2.25 * d1_inv))) + 0.9375 :
                                      (n1 * (x - (2.625 * d1_inv)) * (x - (2.625 * d1_inv))) + 0.984375;
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
