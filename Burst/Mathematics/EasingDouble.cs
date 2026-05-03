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
        /// <summary>Quadratic easing (Entering).</summary>
        public static PRECISION QuadIn(PRECISION x) => QuadIn_Raw(x);
        /// <summary>Quadratic easing (Exiting).</summary>
        public static PRECISION QuadOut(PRECISION x) => QuadOut_Raw(x);
        /// <summary>Quadratic easing (Entering and Exiting).</summary>
        public static PRECISION QuadInOut(PRECISION x) => QuadInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuadIn_Raw(PRECISION x) => x * x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuadOut_Raw(PRECISION x) => 1.0 - ((1.0 - x) * (1.0 - x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuadInOut_Raw(PRECISION x) => x < 0.5 ? 2.0 * x * x : 1.0 - (((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * 0.5);

        /*  Cubic  ================================================================ */
        /// <summary>Cubic easing (Entering).</summary>
        public static PRECISION CubicIn(PRECISION x) => CubicIn_Raw(x);
        /// <summary>Cubic easing (Exiting).</summary>
        public static PRECISION CubicOut(PRECISION x) => CubicOut_Raw(x);
        /// <summary>Cubic easing (Entering and Exiting).</summary>
        public static PRECISION CubicInOut(PRECISION x) => CubicInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CubicIn_Raw(PRECISION x) => x * x * x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CubicOut_Raw(PRECISION x) => 1.0 - ((1.0 - x) * (1.0 - x) * (1.0 - x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CubicInOut_Raw(PRECISION x) => x < 0.5 ? 4.0 * x * x * x : 1.0 - (((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * 0.5);

        /*  Quart  ================================================================ */
        /// <summary>Quartic easing (Entering).</summary>
        public static PRECISION QuartIn(PRECISION x) => QuartIn_Raw(x);
        /// <summary>Quartic easing (Exiting).</summary>
        public static PRECISION QuartOut(PRECISION x) => QuartOut_Raw(x);
        /// <summary>Quartic easing (Entering and Exiting).</summary>
        public static PRECISION QuartInOut(PRECISION x) => QuartInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuartIn_Raw(PRECISION x) => x * x * x * x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuartOut_Raw(PRECISION x) => 1.0 - ((1.0 - x) * (1.0 - x) * (1.0 - x) * (1.0 - x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuartInOut_Raw(PRECISION x) => x < 0.5 ? 8.0 * x * x * x * x : 1.0 - (((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * 0.5);

        /*  Quint  ================================================================ */
        /// <summary>Quintic easing (Entering).</summary>
        public static PRECISION QuintIn(PRECISION x) => QuintIn_Raw(x);
        /// <summary>Quintic easing (Exiting).</summary>
        public static PRECISION QuintOut(PRECISION x) => QuintOut_Raw(x);
        /// <summary>Quintic easing (Entering and Exiting).</summary>
        public static PRECISION QuintInOut(PRECISION x) => QuintInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuintIn_Raw(PRECISION x) => x * x * x * x * x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuintOut_Raw(PRECISION x) => 1.0 - ((1.0 - x) * (1.0 - x) * (1.0 - x) * (1.0 - x) * (1.0 - x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuintInOut_Raw(PRECISION x) => x < 0.5 ? 16.0 * x * x * x * x * x : 1.0 - (((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * ((-2.0 * x) + 2.0) * 0.5);

        /*  Sine  ================================================================ */
        /// <summary>Sinusoidal easing (Entering).</summary>
        public static PRECISION SineIn(PRECISION x) => SineIn_Raw(x);
        /// <summary>Sinusoidal easing (Exiting).</summary>
        public static PRECISION SineOut(PRECISION x) => SineOut_Raw(x);
        /// <summary>Sinusoidal easing (Entering and Exiting).</summary>
        public static PRECISION SineInOut(PRECISION x) => SineInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION SineIn_Raw(PRECISION x) => 1.0 - Sin5((1.0 - x) * SYSMATH.PI * 0.5);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION SineOut_Raw(PRECISION x) => Sin5(x * SYSMATH.PI * 0.5);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION SineInOut_Raw(PRECISION x) => (1.0 - Sin5((0.5 - x) * SYSMATH.PI)) * 0.5;

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

        /*  Expo  ================================================================ */
        /// <summary>Exponential easing (Entering).</summary>
        public static PRECISION ExpoIn(PRECISION x) => ExpoIn_Raw(x);
        /// <summary>Exponential easing (Exiting).</summary>
        public static PRECISION ExpoOut(PRECISION x) => ExpoOut_Raw(x);
        /// <summary>Exponential easing (Entering and Exiting).</summary>
        public static PRECISION ExpoInOut(PRECISION x) => ExpoInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ExpoIn_Raw(PRECISION x) => x <= 0.0 ? 0.0 : Math.exp(((10.0 * x) - 10.0) * ln_2);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ExpoOut_Raw(PRECISION x) => x >= 1.0 ? 1.0 : 1.0 - Math.exp((-10.0 * x) * ln_2);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ExpoInOut_Raw(PRECISION x) => x <= 0.0 ? 0.0 : (x >= 1.0 ? 1.0 : x < 0.5 ? Math.exp(((20.0 * x) - 10.0) * ln_2) * 0.5 : (2.0 - Math.exp((10.0 - (20.0 * x)) * ln_2)) * 0.5);

        private const PRECISION ln_2 = 0.6931471805599453;

        /*  Circ  ================================================================ */
        /// <summary>Circular easing (Entering).</summary>
        public static PRECISION CircIn(PRECISION x) => CircIn_Raw(x);
        /// <summary>Circular easing (Exiting).</summary>
        public static PRECISION CircOut(PRECISION x) => CircOut_Raw(x);
        /// <summary>Circular easing (Entering and Exiting).</summary>
        public static PRECISION CircInOut(PRECISION x) => CircInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CircIn_Raw(PRECISION x) => 1.0 - FastSqrt(1.0 - (x * x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CircOut_Raw(PRECISION x) => FastSqrt(1.0 - ((x - 1.0) * (x - 1.0)));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CircInOut_Raw(PRECISION x) => x < 0.5 ? (1.0 - FastSqrt(1.0 - ((2.0 * x) * (2.0 * x)))) * 0.5 : (FastSqrt(1.0 - ((2.0 - (2.0 * x)) * (2.0 - (2.0 * x)))) + 1.0) * 0.5;

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

        /*  Back  ================================================================ */
        /// <summary>Back easing (overshooting) (Entering).</summary>
        public static PRECISION BackIn(PRECISION x) => BackIn_Raw(x);
        /// <summary>Back easing (overshooting) (Exiting).</summary>
        public static PRECISION BackOut(PRECISION x) => BackOut_Raw(x);
        /// <summary>Back easing (overshooting) (Entering and Exiting).</summary>
        public static PRECISION BackInOut(PRECISION x) => BackInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BackIn_Raw(PRECISION x) => ((c1 + 1.0) * x * x * x) - (c1 * x * x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BackOut_Raw(PRECISION x) => 1.0 + ((c1 + 1.0) * (x - 1.0) * (x - 1.0) * (x - 1.0)) + (c1 * (x - 1.0) * (x - 1.0));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BackInOut_Raw(PRECISION x) => x < 0.5 ? ((2.0 * x) * (2.0 * x) * (((c2 + 1.0) * (2.0 * x)) - c2)) * 0.5 : (((2.0 * x - 2.0) * (2.0 * x - 2.0) * (((c2 + 1.0) * (2.0 * x - 2.0)) + c2)) + 2.0) * 0.5;

        private const PRECISION c1 = 1.70158;
        private const PRECISION c2 = c1 * 1.525;

        /*  Elastic  ================================================================ */
        /// <summary>Elastic easing (oscillating spring) (Entering).</summary>
        public static PRECISION ElasticIn(PRECISION x) => ElasticIn_Raw(x);
        /// <summary>Elastic easing (oscillating spring) (Exiting).</summary>
        public static PRECISION ElasticOut(PRECISION x) => ElasticOut_Raw(x);
        /// <summary>Elastic easing (oscillating spring) (Entering and Exiting).</summary>
        public static PRECISION ElasticInOut(PRECISION x) => ElasticInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ElasticIn_Raw(PRECISION x) => x <= 0.0 ? 0.0 : (x >= 1.0 ? 1.0 : -Math.exp(((10.0 * x) - 10.0) * ln_2) * Math.sin(((10.0 * x) - 10.75) * e_c4));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ElasticOut_Raw(PRECISION x) => x <= 0.0 ? 0.0 : (x >= 1.0 ? 1.0 : (Math.exp((-10.0 * x) * ln_2) * Math.sin(((10.0 * x) - 0.75) * e_c4)) + 1.0);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ElasticInOut_Raw(PRECISION x) => x <= 0.0 ? 0.0 : (x >= 1.0 ? 1.0 : x < 0.5 ? -Math.exp(((20.0 * x) - 10.0) * ln_2) * Math.sin(((20.0 * x) - 11.125) * e_c5) * 0.5 : (Math.exp((10.0 - (20.0 * x)) * ln_2) * Math.sin(((20.0 * x) - 11.125) * e_c5) * 0.5) + 1.0);

        private const PRECISION e_c4 = 2.0 * SYSMATH.PI / 3.0;
        private const PRECISION e_c5 = 2.0 * SYSMATH.PI / 4.5;

        /*  Bounce  ================================================================ */
        /// <summary>Bounce easing (simulated gravity/collision) (Entering).</summary>
        public static PRECISION BounceIn(PRECISION x) => BounceIn_Raw(x);
        /// <summary>Bounce easing (simulated gravity/collision) (Exiting).</summary>
        public static PRECISION BounceOut(PRECISION x) => BounceOut_Raw(x);
        /// <summary>Bounce easing (simulated gravity/collision) (Entering and Exiting).</summary>
        public static PRECISION BounceInOut(PRECISION x) => BounceInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BounceIn_Raw(PRECISION x) => 1.0 - BounceOut_Impl(1.0 - x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BounceOut_Raw(PRECISION x) => BounceOut_Impl(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BounceInOut_Raw(PRECISION x) => x < 0.5 ? (1.0 - BounceOut_Impl(1.0 - (2.0 * x))) * 0.5 : (1.0 + BounceOut_Impl((2.0 * x) - 1.0)) * 0.5;

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
    }
}
