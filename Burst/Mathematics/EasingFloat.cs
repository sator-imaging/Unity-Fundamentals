// High Performance Easing Functions for Unity
// (c) 2026 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

// Basic Usage (Use LerpUnclamped):
//   float invDuration = 1.0f / duration;
//   float t = (elapsedTime += Time.deltaTime) * invDuration;
//   Math.LerpUnclamped(from, to, easing(x));

// How to Convert from EasingDouble:
// - Regex to replace number literals
//   - (?<![\w.])(\d+(?:\.\d+)?)(?![\w.]*f)
//   --> $1f (append 'f' suffix)
//   --> Revert copyright year
// - Convert 'long' to 'int'
//   - Magic number is different --> 0x5f3700a0
//   --> https://qiita.com/metaphysical_bard/items/e04378b16d6173127435

/// DEBUG
//#undef STMG_UNITYMATH_EXISTS
//#undef STMG_BURST_EXISTS

/// PRECISION SETTINGS
#define __burst_float

using PRECISION = System.Single;
using INTEGER = System.Int32;
using SYSMATH = UnityEngine.Mathf;

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
    public struct EasingFloat
    {
        /*  Quad  ================================================================ */
        /// <summary>Quadratic easing (Entering).</summary>
        public static PRECISION QuadIn(PRECISION x) => QuadIn_Raw(x);
        /// <summary>Quadratic easing (Exiting).</summary>
        public static PRECISION QuadOut(PRECISION x) => QuadOut_Raw(x);
        /// <summary>Quadratic easing (Entering and Exiting).</summary>
        public static PRECISION QuadInOut(PRECISION x) => QuadInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuadIn_Raw(PRECISION x) => x * x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuadOut_Raw(PRECISION x) => 1.0f - ((1.0f - x) * (1.0f - x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuadInOut_Raw(PRECISION x) => x < 0.5f ? 2.0f * x * x : 1.0f - (((-2.0f * x) + 2.0f) * ((-2.0f * x) + 2.0f) * 0.5f);

        /*  Cubic  ================================================================ */
        /// <summary>Cubic easing (Entering).</summary>
        public static PRECISION CubicIn(PRECISION x) => CubicIn_Raw(x);
        /// <summary>Cubic easing (Exiting).</summary>
        public static PRECISION CubicOut(PRECISION x) => CubicOut_Raw(x);
        /// <summary>Cubic easing (Entering and Exiting).</summary>
        public static PRECISION CubicInOut(PRECISION x) => CubicInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CubicIn_Raw(PRECISION x) => x * x * x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CubicOut_Raw(PRECISION x) => 1.0f - ((1.0f - x) * (1.0f - x) * (1.0f - x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CubicInOut_Raw(PRECISION x) => x < 0.5f ? 4.0f * x * x * x : 1.0f - (((-2.0f * x) + 2.0f) * ((-2.0f * x) + 2.0f) * ((-2.0f * x) + 2.0f) * 0.5f);

        /*  Quart  ================================================================ */
        /// <summary>Quartic easing (Entering).</summary>
        public static PRECISION QuartIn(PRECISION x) => QuartIn_Raw(x);
        /// <summary>Quartic easing (Exiting).</summary>
        public static PRECISION QuartOut(PRECISION x) => QuartOut_Raw(x);
        /// <summary>Quartic easing (Entering and Exiting).</summary>
        public static PRECISION QuartInOut(PRECISION x) => QuartInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuartIn_Raw(PRECISION x) => x * x * x * x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuartOut_Raw(PRECISION x) => 1.0f - ((1.0f - x) * (1.0f - x) * (1.0f - x) * (1.0f - x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuartInOut_Raw(PRECISION x) => x < 0.5f ? 8.0f * x * x * x * x : 1.0f - (((-2.0f * x) + 2.0f) * ((-2.0f * x) + 2.0f) * ((-2.0f * x) + 2.0f) * ((-2.0f * x) + 2.0f) * 0.5f);

        /*  Quint  ================================================================ */
        /// <summary>Quintic easing (Entering).</summary>
        public static PRECISION QuintIn(PRECISION x) => QuintIn_Raw(x);
        /// <summary>Quintic easing (Exiting).</summary>
        public static PRECISION QuintOut(PRECISION x) => QuintOut_Raw(x);
        /// <summary>Quintic easing (Entering and Exiting).</summary>
        public static PRECISION QuintInOut(PRECISION x) => QuintInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuintIn_Raw(PRECISION x) => x * x * x * x * x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuintOut_Raw(PRECISION x) => 1.0f - ((1.0f - x) * (1.0f - x) * (1.0f - x) * (1.0f - x) * (1.0f - x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION QuintInOut_Raw(PRECISION x) => x < 0.5f ? 16.0f * x * x * x * x * x : 1.0f - (((-2.0f * x) + 2.0f) * ((-2.0f * x) + 2.0f) * ((-2.0f * x) + 2.0f) * ((-2.0f * x) + 2.0f) * ((-2.0f * x) + 2.0f) * 0.5f);

        /*  Sine  ================================================================ */
        /// <summary>Sinusoidal easing (Entering).</summary>
        public static PRECISION SineIn(PRECISION x) => SineIn_Raw(x);
        /// <summary>Sinusoidal easing (Exiting).</summary>
        public static PRECISION SineOut(PRECISION x) => SineOut_Raw(x);
        /// <summary>Sinusoidal easing (Entering and Exiting).</summary>
        public static PRECISION SineInOut(PRECISION x) => SineInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION SineIn_Raw(PRECISION x) => 1.0f - Sin5((1.0f - x) * SYSMATH.PI * 0.5f);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION SineOut_Raw(PRECISION x) => Sin5(x * SYSMATH.PI * 0.5f);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION SineInOut_Raw(PRECISION x) => (1.0f - Sin5((0.5f - x) * SYSMATH.PI)) * 0.5f;

        // x5 faster but low quality especially for Elastic (in slo mo, less motion continuity)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION Sin5(PRECISION t)
        {
            const PRECISION inv6 = 1.0f / 6.0f;

            // Original formula: sin(t) ≈ t - t^3/6 + t^5/120
            // Targeted formula: sin(t) ≈ t - t^3/6 + t^5/C
            // Constant C is adjusted to fit 0..1 range as possible (targeted sin(PI/2) = 1).
            const PRECISION invC = 1.0f / 127.2236176f;

            return t - (t * t * t * inv6) + (t * t * t * t * t * invC);
        }

        /*  Expo  ================================================================ */
        /// <summary>Exponential easing (Entering).</summary>
        public static PRECISION ExpoIn(PRECISION x) => ExpoIn_Raw(x);
        /// <summary>Exponential easing (Exiting).</summary>
        public static PRECISION ExpoOut(PRECISION x) => ExpoOut_Raw(x);
        /// <summary>Exponential easing (Entering and Exiting).</summary>
        public static PRECISION ExpoInOut(PRECISION x) => ExpoInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ExpoIn_Raw(PRECISION x) => x <= 0.0f ? 0.0f : Math.exp(((10.0f * x) - 10.0f) * ln_2);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ExpoOut_Raw(PRECISION x) => x >= 1.0f ? 1.0f : 1.0f - Math.exp((-10.0f * x) * ln_2);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ExpoInOut_Raw(PRECISION x) => x <= 0.0f ? 0.0f : (x >= 1.0f ? 1.0f : x < 0.5f ? Math.exp(((20.0f * x) - 10.0f) * ln_2) * 0.5f : (2.0f - Math.exp((10.0f - (20.0f * x)) * ln_2)) * 0.5f);

        private const PRECISION ln_2 = 0.6931471805599453f;

        /*  Circ  ================================================================ */
        /// <summary>Circular easing (Entering).</summary>
        public static PRECISION CircIn(PRECISION x) => CircIn_Raw(x);
        /// <summary>Circular easing (Exiting).</summary>
        public static PRECISION CircOut(PRECISION x) => CircOut_Raw(x);
        /// <summary>Circular easing (Entering and Exiting).</summary>
        public static PRECISION CircInOut(PRECISION x) => CircInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CircIn_Raw(PRECISION x) => 1.0f - FastSqrt(1.0f - (x * x));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CircOut_Raw(PRECISION x) => FastSqrt(1.0f - ((x - 1.0f) * (x - 1.0f)));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION CircInOut_Raw(PRECISION x) => x < 0.5f ? (1.0f - FastSqrt(1.0f - ((2.0f * x) * (2.0f * x)))) * 0.5f : (FastSqrt(1.0f - ((2.0f - (2.0f * x)) * (2.0f - (2.0f * x)))) + 1.0f) * 0.5f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION FastSqrt(PRECISION x)
        {
            PRECISION xHalf = x * 0.5f;
            INTEGER i = BitConverter.SingleToInt32Bits(x);
            i = 0x5f3700a0 - (i >> 1);

            PRECISION y = BitConverter.Int32BitsToSingle(i);

            // Original formula: y = y * (1.5 - xHalf * y * y)
            // Targeted formula: y = y * (K - xHalf * y * y)
            // Constant K is adjusted to fit 0..1 range as possible (targeted sqrt(1) = 1).
            y *= 1.501898050f - (xHalf * y * y);

            return x <= 0.0f ? 0.0f : x * y;
        }

        /*  Back  ================================================================ */
        /// <summary>Back easing (overshooting) (Entering).</summary>
        public static PRECISION BackIn(PRECISION x) => BackIn_Raw(x);
        /// <summary>Back easing (overshooting) (Exiting).</summary>
        public static PRECISION BackOut(PRECISION x) => BackOut_Raw(x);
        /// <summary>Back easing (overshooting) (Entering and Exiting).</summary>
        public static PRECISION BackInOut(PRECISION x) => BackInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BackIn_Raw(PRECISION x) => ((c1 + 1.0f) * x * x * x) - (c1 * x * x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BackOut_Raw(PRECISION x) => 1.0f + ((c1 + 1.0f) * (x - 1.0f) * (x - 1.0f) * (x - 1.0f)) + (c1 * (x - 1.0f) * (x - 1.0f));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BackInOut_Raw(PRECISION x) => x < 0.5f ? ((2.0f * x) * (2.0f * x) * (((c2 + 1.0f) * (2.0f * x)) - c2)) * 0.5f : (((2.0f * x - 2.0f) * (2.0f * x - 2.0f) * (((c2 + 1.0f) * (2.0f * x - 2.0f)) + c2)) + 2.0f) * 0.5f;

        private const PRECISION c1 = 1.70158f;
        private const PRECISION c2 = c1 * 1.525f;

        /*  Elastic  ================================================================ */
        /// <summary>Elastic easing (oscillating spring) (Entering).</summary>
        public static PRECISION ElasticIn(PRECISION x) => ElasticIn_Raw(x);
        /// <summary>Elastic easing (oscillating spring) (Exiting).</summary>
        public static PRECISION ElasticOut(PRECISION x) => ElasticOut_Raw(x);
        /// <summary>Elastic easing (oscillating spring) (Entering and Exiting).</summary>
        public static PRECISION ElasticInOut(PRECISION x) => ElasticInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ElasticIn_Raw(PRECISION x) => x <= 0.0f ? 0.0f : (x >= 1.0f ? 1.0f : -Math.exp(((10.0f * x) - 10.0f) * ln_2) * Math.sin(((10.0f * x) - 10.75f) * e_c4));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ElasticOut_Raw(PRECISION x) => x <= 0.0f ? 0.0f : (x >= 1.0f ? 1.0f : (Math.exp((-10.0f * x) * ln_2) * Math.sin(((10.0f * x) - 0.75f) * e_c4)) + 1.0f);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION ElasticInOut_Raw(PRECISION x) => x <= 0.0f ? 0.0f : (x >= 1.0f ? 1.0f : x < 0.5f ? -Math.exp(((20.0f * x) - 10.0f) * ln_2) * Math.sin(((20.0f * x) - 11.125f) * e_c5) * 0.5f : (Math.exp((10.0f - (20.0f * x)) * ln_2) * Math.sin(((20.0f * x) - 11.125f) * e_c5) * 0.5f) + 1.0f);

        private const PRECISION e_c4 = 2.0f * SYSMATH.PI / 3.0f;
        private const PRECISION e_c5 = 2.0f * SYSMATH.PI / 4.5f;

        /*  Bounce  ================================================================ */
        /// <summary>Bounce easing (simulated gravity/collision) (Entering).</summary>
        public static PRECISION BounceIn(PRECISION x) => BounceIn_Raw(x);
        /// <summary>Bounce easing (simulated gravity/collision) (Exiting).</summary>
        public static PRECISION BounceOut(PRECISION x) => BounceOut_Raw(x);
        /// <summary>Bounce easing (simulated gravity/collision) (Entering and Exiting).</summary>
        public static PRECISION BounceInOut(PRECISION x) => BounceInOut_Raw(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BounceIn_Raw(PRECISION x) => 1.0f - BounceOut_Impl(1.0f - x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BounceOut_Raw(PRECISION x) => BounceOut_Impl(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static PRECISION BounceInOut_Raw(PRECISION x) => x < 0.5f ? (1.0f - BounceOut_Impl(1.0f - (2.0f * x))) * 0.5f : (1.0f + BounceOut_Impl((2.0f * x) - 1.0f)) * 0.5f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION BounceOut_Impl(PRECISION x)
        {
            const PRECISION n1 = 7.5625f;
            const PRECISION d1_inv = 1.0f / 2.75f;
            return x < 1.0f * d1_inv ? n1 * x * x :
                   x < 2.0f * d1_inv ? (n1 * (x - (1.5f * d1_inv)) * (x - (1.5f * d1_inv))) + 0.75f :
                   x < 2.5f * d1_inv ? (n1 * (x - (2.25f * d1_inv)) * (x - (2.25f * d1_inv))) + 0.9375f :
                                      (n1 * (x - (2.625f * d1_inv)) * (x - (2.625f * d1_inv))) + 0.984375f;
        }
    }
}
