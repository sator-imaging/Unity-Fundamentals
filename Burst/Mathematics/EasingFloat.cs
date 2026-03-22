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



// PRECISION SETTINGS

#define __burst_float

using PRECISION = System.Single;
using INTEGER = System.Int32;
using SYSMATH = UnityEngine.Mathf;



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
    public struct EasingFloat
    {
        /*  Quad  ================================================================ */

        /// <summary><c>=> pow(x, 2f)</c></summary>
        public static PRECISION QuadIn(PRECISION x) => x * x;
        public static PRECISION QuadOut(PRECISION x)
        {
            PRECISION t = 1.0f - x;
            return 1.0f - (t * t);
        }
        public static PRECISION QuadInOut(PRECISION x)
        {
            if (x < 0.5f)
            {
                return 2.0f * x * x;
            }

            PRECISION t = (-2.0f * x) + 2.0f;
            return 1.0f - (t * t * 0.5f);
        }


        /*  Cubic  ================================================================ */

        /// <summary><c>=> pow(x, 3f)</c></summary>
        public static PRECISION CubicIn(PRECISION x) => x * x * x;
        public static PRECISION CubicOut(PRECISION x)
        {
            PRECISION t = 1.0f - x;
            return 1.0f - (t * t * t);
        }
        public static PRECISION CubicInOut(PRECISION x)
        {
            if (x < 0.5f)
            {
                return 4.0f * x * x * x;
            }

            PRECISION t = (-2.0f * x) + 2.0f;
            return 1.0f - (t * t * t * 0.5f);
        }


        /*  Quart  ================================================================ */

        /// <summary><c>=> pow(x, 4f)</c></summary>
        public static PRECISION QuartIn(PRECISION x) => x * x * x * x;
        public static PRECISION QuartOut(PRECISION x)
        {
            PRECISION t = 1.0f - x;
            return 1.0f - (t * t * t * t);
        }
        public static PRECISION QuartInOut(PRECISION x)
        {
            if (x < 0.5f)
            {
                return 8.0f * x * x * x * x;
            }

            PRECISION t = (-2.0f * x) + 2.0f;
            PRECISION t2 = t * t;
            return 1.0f - (t2 * t2 * 0.5f);
        }


        /*  Quint  ================================================================ */

        /// <summary><c>=> pow(x, 5f)</c></summary>
        public static PRECISION QuintIn(PRECISION x) => x * x * x * x * x;
        public static PRECISION QuintOut(PRECISION x)
        {
            PRECISION t = 1.0f - x;
            return 1.0f - (t * t * t * t * t);
        }
        public static PRECISION QuintInOut(PRECISION x)
        {
            if (x < 0.5f)
            {
                return 16.0f * x * x * x * x * x;
            }

            PRECISION t = (-2.0f * x) + 2.0f;
            PRECISION t2 = t * t;
            return 1.0f - (t2 * t2 * t * 0.5f);
        }


        /*  Sine  ================================================================ */

        // x5 faster but low quality especially for Elastic (in slo mo, less motion continuity)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION Sin5(PRECISION t)
        {
            const PRECISION inv6 = 1.0f / 6.0f;
            const PRECISION inv120 = 1.0f / 120.0f;

            // sin(t) ≈ t - t^3f/6f + t^5f/120f
            PRECISION t2 = t * t;
            PRECISION t3 = t2 * t;
            PRECISION t5 = t3 * t2;
            return t - (t3 * inv6) + (t5 * inv120);
        }

        /// <summary>Uses approximate sine value.</summary>
        public static PRECISION SineIn(PRECISION x) => 1.0f - Sin5((1.0f - x) * Math.PI * 0.5f);
        public static PRECISION SineOut(PRECISION x) => Sin5(x * Math.PI * 0.5f);
        public static PRECISION SineInOut(PRECISION x) => (1.0f - Sin5((0.5f - x) * Math.PI)) * 0.5f;


        /*  Expo  ================================================================ */

        private const PRECISION ln_2 = 0.6931471805599453f;

        /// <summary><c>=> pow(2.0f, 10.0f * x - 10.0f)</c></summary>
        public static PRECISION ExpoIn(PRECISION x) => x == 0.0f ? 0.0f : Math.exp(((10.0f * x) - 10.0f) * ln_2);
        public static PRECISION ExpoOut(PRECISION x) => x == 1.0f ? 1.0f : 1.0f - Math.exp((-10.0f * x) * ln_2);
        public static PRECISION ExpoInOut(PRECISION x)
        {
            if (x == 0.0f)
            {
                return 0.0f;
            }
            if (x == 1.0f)
            {
                return 1.0f;
            }

            PRECISION t = (20.0f * x) - 10.0f;
            return x < 0.5f
                ? Math.exp(t * ln_2) * 0.5f
                : (2.0f - Math.exp((-t) * ln_2)) * 0.5f;
        }


        /*  Circ  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION FastSqrt(PRECISION x)
        {
            if (x <= 0.0f)
            {
                return 0.0f;
            }

            PRECISION xHalf = x * 0.5f;
            INTEGER i = BitConverter.SingleToInt32Bits(x);
            i = 0x5f3700a0 - (i >> 1);

            PRECISION y = BitConverter.Int32BitsToSingle(i);
            y *= 1.5f - (xHalf * y * y);
            return x * y;
        }

        /// <summary><c>=> 1.0f - sqrt(1.0f - x * x)</c></summary>
        public static PRECISION CircIn(PRECISION x) => 1.0f - FastSqrt(1.0f - (x * x));
        public static PRECISION CircOut(PRECISION x)
        {
            PRECISION t = x - 1.0f;
            return FastSqrt(1.0f - (t * t));
        }
        public static PRECISION CircInOut(PRECISION x)
        {
            PRECISION t = 2.0f * x;
            if (x < 0.5f)
            {
                return (1.0f - FastSqrt(1.0f - (t * t))) * 0.5f;
            }

            PRECISION u = 2.0f - t;
            return (FastSqrt(1.0f - (u * u)) + 1.0f) * 0.5f;
        }


        /*  Back  ================================================================ */

        private const PRECISION c1 = 1.70158f;
        private const PRECISION c2 = c1 * 1.525f;

        /// <summary>Overshoots the range once and returns.</summary>
        public static PRECISION BackIn(PRECISION x) => ((c1 + 1.0f) * x * x * x) - (c1 * x * x);
        public static PRECISION BackOut(PRECISION x)
        {
            PRECISION t = x - 1.0f;
            return 1.0f + ((c1 + 1.0f) * t * t * t) + (c1 * t * t);
        }
        public static PRECISION BackInOut(PRECISION x)
        {
            PRECISION t = 2.0f * x;
            if (x < 0.5f)
            {
                return (t * t * (((c2 + 1.0f) * t) - c2)) * 0.5f;
            }

            PRECISION u = t - 2.0f;
            return ((u * u * (((c2 + 1.0f) * u) + c2)) + 2.0f) * 0.5f;
        }


        /*  Elastic  ================================================================ */

        private const PRECISION e_c4 = 2.0f * Math.PI / 3.0f;
        private const PRECISION e_c5 = 2.0f * Math.PI / 4.5f;

        /// <summary>Simulates spring-like oscillations.</summary>
        public static PRECISION ElasticIn(PRECISION x)
        {
            if (x == 0.0f)
            {
                return 0.0f;
            }

            if (x == 1.0f)
            {
                return 1.0f;
            }

            PRECISION t = 10.0f * x;
            return -Math.exp((t - 10.0f) * ln_2) * Math.sin((t - 10.75f) * e_c4);
        }

        public static PRECISION ElasticOut(PRECISION x)
        {
            if (x == 0.0f)
            {
                return 0.0f;
            }

            if (x == 1.0f)
            {
                return 1.0f;
            }

            PRECISION t = 10.0f * x;
            return (Math.exp((-t) * ln_2) * Math.sin((t - 0.75f) * e_c4)) + 1.0f;
        }

        public static PRECISION ElasticInOut(PRECISION x)
        {
            if (x == 0.0f)
            {
                return 0.0f;
            }

            if (x == 1.0f)
            {
                return 1.0f;
            }

            PRECISION t = (20.0f * x) - 10.0f;
            PRECISION angle = (t - 1.125f) * e_c5;
            PRECISION s = Math.sin(angle) * 0.5f;

            if (x < 0.5f)
            {
                return -Math.exp(t * ln_2) * s;
            }

            return (Math.exp(-t * ln_2) * s) + 1.0f;
        }


        /*  Bounce  ================================================================ */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PRECISION BounceOut_Impl(PRECISION x)
        {
            const PRECISION n1 = 7.5625f;
            const PRECISION d1_inv = 1.0f / 2.75f;
            if (x < 1.0f * d1_inv)
            {
                return n1 * x * x;
            }
            else if (x < 2.0f * d1_inv)
            {
                x -= 1.5f * d1_inv;
                return (n1 * x * x) + 0.75f;
            }
            else if (x < 2.5f * d1_inv)
            {
                x -= 2.25f * d1_inv;
                return (n1 * x * x) + 0.9375f;
            }
            else
            {
                x -= 2.625f * d1_inv;
                return (n1 * x * x) + 0.984375f;
            }
        }

        /// <summary>Simulates a bouncing motion against a boundary.</summary>
        public static PRECISION BounceIn(PRECISION x) => 1.0f - BounceOut_Impl(1.0f - x);
        public static PRECISION BounceOut(PRECISION x) => BounceOut_Impl(x);
        public static PRECISION BounceInOut(PRECISION x)
        {
            PRECISION t = 2.0f * x;
            return x < 0.5f ? (1.0f - BounceOut_Impl(1.0f - t)) * 0.5f : (1.0f + BounceOut_Impl(t - 1.0f)) * 0.5f;
        }
    }
}
