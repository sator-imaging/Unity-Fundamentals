// High Performance Easing Functions for Unity
// (c) 2026 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

// Basic Usage (Use LerpUnclamped):
//   float invDuration = 1.0f / duration;
//   float t = (elapsedTime += Time.deltaTime) * invDuration;
//   Math.LerpUnclamped(from, to, easing(x));

// How to Convert from EasingDouble:
// - Replace
//   - _double_ (no underscore) -> float
//   - Math.Sin -> Math.sin
//   - Math.Exp -> Math.exp
// - Regex to replace number literals
//   - (?<![\w.])(\d+(?:\.\d+)?)(?![\w.]*f)
//   --> $1f (append 'f' suffix)
//   --> Revert copyright year
// - Convert 'long' to 'int'
//   - Magic number is different: 0x5f3700a0
//   --> https://qiita.com/metaphysical_bard/items/e04378b16d6173127435

using System;

#if STMG_UNITYMATH_EXISTS
using Math = Unity.Mathematics.math;
#else
#pragma warning disable IDE1006  // Naming Styles
using Math = SatorImaging.UnityFundamentals.UnityMathPolyfills;
using System.Runtime.CompilerServices;
namespace SatorImaging.UnityFundamentals
{
    internal static class UnityMathPolyfills
    {
        public const float PI = Unity.Mathematics.math.PI;

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float exp(float x) => UnityEngine.Mathf.Exp(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float sin(float x) => UnityEngine.Mathf.Sin(x);
    }
}
#pragma warning restore IDE1006
#endif

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    public static class EasingFloat
    {
        /*  Quad  ================================================================ */
        /// <summary><c>=> pow(x, 2f)</c></summary>
        public static float QuadIn(float x) => x * x;
        public static float QuadOut(float x)
        {
            float t = 1.0f - x;
            return 1.0f - (t * t);
        }
        public static float QuadInOut(float x)
        {
            if (x < 0.5f)
            {
                return 2.0f * x * x;
            }

            float t = (-2.0f * x) + 2.0f;
            return 1.0f - (t * t * 0.5f);
        }


        /*  Cubic  ================================================================ */
        /// <summary><c>=> pow(x, 3f)</c></summary>
        public static float CubicIn(float x) => x * x * x;
        public static float CubicOut(float x)
        {
            float t = 1.0f - x;
            return 1.0f - (t * t * t);
        }
        public static float CubicInOut(float x)
        {
            if (x < 0.5f)
            {
                return 4.0f * x * x * x;
            }

            float t = (-2.0f * x) + 2.0f;
            return 1.0f - (t * t * t * 0.5f);
        }


        /*  Quart  ================================================================ */
        /// <summary><c>=> pow(x, 4f)</c></summary>
        public static float QuartIn(float x) => x * x * x * x;
        public static float QuartOut(float x)
        {
            float t = 1.0f - x;
            return 1.0f - (t * t * t * t);
        }
        public static float QuartInOut(float x)
        {
            if (x < 0.5f)
            {
                return 8.0f * x * x * x * x;
            }

            float t = (-2.0f * x) + 2.0f;
            float t2 = t * t;
            return 1.0f - (t2 * t2 * 0.5f);
        }


        /*  Quint  ================================================================ */
        /// <summary><c>=> pow(x, 5f)</c></summary>
        public static float QuintIn(float x) => x * x * x * x * x;
        public static float QuintOut(float x)
        {
            float t = 1.0f - x;
            return 1.0f - (t * t * t * t * t);
        }
        public static float QuintInOut(float x)
        {
            if (x < 0.5f)
            {
                return 16.0f * x * x * x * x * x;
            }

            float t = (-2.0f * x) + 2.0f;
            float t2 = t * t;
            return 1.0f - (t2 * t2 * t * 0.5f);
        }


        /*  Sine  ================================================================ */
        // x5 faster but low quality especially for Elastic (in slo mo, less motion continuity)
        private static float Sin5(float t)
        {
            const float inv6 = 1.0f / 6.0f;
            const float inv120 = 1.0f / 120.0f;

            // sin(t) ≈ t - t^3f/6f + t^5f/120f
            float t2 = t * t;
            float t3 = t2 * t;
            float t5 = t3 * t2;
            return t - (t3 * inv6) + (t5 * inv120);
        }

        /// <summary>Uses approximate sine value.</summary>
        public static float SineIn(float x) => 1.0f - Sin5((1.0f - x) * Math.PI * 0.5f);
        public static float SineOut(float x) => Sin5(x * Math.PI * 0.5f);
        public static float SineInOut(float x) => (1.0f - Sin5((0.5f - x) * Math.PI)) * 0.5f;


        /*  Expo  ================================================================ */
        private const float ln_2 = 0.6931471805599453f;

        /// <summary><c>=> pow(2.0f, 10.0f * x - 10.0f)</c></summary>
        public static float ExpoIn(float x) => x == 0.0f ? 0.0f : Math.exp(((10.0f * x) - 10.0f) * ln_2);
        public static float ExpoOut(float x) => x == 1.0f ? 1.0f : 1.0f - Math.exp((-10.0f * x) * ln_2);
        public static float ExpoInOut(float x)
        {
            if (x == 0.0f)
            {
                return 0.0f;
            }
            if (x == 1.0f)
            {
                return 1.0f;
            }

            float t = (20.0f * x) - 10.0f;
            return x < 0.5f
                ? Math.exp(t * ln_2) * 0.5f
                : (2.0f - Math.exp((-t) * ln_2)) * 0.5f;
        }


        /*  Circ  ================================================================ */
        private static float FastSqrt(float x)
        {
            if (x <= 0.0f)
            {
                return 0.0f;
            }

            float xHalf = x * 0.5f;
            int i = BitConverter.SingleToInt32Bits(x);
            i = 0x5f3700a0 - (i >> 1);

            float y = BitConverter.Int32BitsToSingle(i);
            y *= 1.5f - (xHalf * y * y);
            return x * y;
        }

        /// <summary><c>=> 1.0f - sqrt(1.0f - x * x)</c></summary>
        public static float CircIn(float x) => 1.0f - FastSqrt(1.0f - (x * x));
        public static float CircOut(float x)
        {
            float t = x - 1.0f;
            return FastSqrt(1.0f - (t * t));
        }
        public static float CircInOut(float x)
        {
            float t = 2.0f * x;
            if (x < 0.5f)
            {
                return (1.0f - FastSqrt(1.0f - (t * t))) * 0.5f;
            }

            float u = 2.0f - t;
            return (FastSqrt(1.0f - (u * u)) + 1.0f) * 0.5f;
        }


        /*  Back  ================================================================ */
        private const float c1 = 1.70158f;
        private const float c2 = c1 * 1.525f;

        /// <summary>Overshoots the range once and returns.</summary>
        public static float BackIn(float x) => ((c1 + 1.0f) * x * x * x) - (c1 * x * x);
        public static float BackOut(float x)
        {
            float t = x - 1.0f;
            return 1.0f + ((c1 + 1.0f) * t * t * t) + (c1 * t * t);
        }
        public static float BackInOut(float x)
        {
            float t = 2.0f * x;
            if (x < 0.5f)
            {
                return (t * t * (((c2 + 1.0f) * t) - c2)) * 0.5f;
            }

            float u = t - 2.0f;
            return ((u * u * (((c2 + 1.0f) * u) + c2)) + 2.0f) * 0.5f;
        }


        /*  Elastic  ================================================================ */
        private const float e_c4 = 2.0f * Math.PI / 3.0f;
        private const float e_c5 = 2.0f * Math.PI / 4.5f;

        /// <summary>Simulates spring-like oscillations.</summary>
        public static float ElasticIn(float x)
        {
            if (x == 0.0f)
            {
                return 0.0f;
            }

            if (x == 1.0f)
            {
                return 1.0f;
            }

            float t = 10.0f * x;
            return -Math.exp((t - 10.0f) * ln_2) * Math.sin((t - 10.75f) * e_c4);
        }

        public static float ElasticOut(float x)
        {
            if (x == 0.0f)
            {
                return 0.0f;
            }

            if (x == 1.0f)
            {
                return 1.0f;
            }

            float t = 10.0f * x;
            return (Math.exp((-t) * ln_2) * Math.sin((t - 0.75f) * e_c4)) + 1.0f;
        }

        public static float ElasticInOut(float x)
        {
            if (x == 0.0f)
            {
                return 0.0f;
            }

            if (x == 1.0f)
            {
                return 1.0f;
            }

            float t = (20.0f * x) - 10.0f;
            float angle = (t - 1.125f) * e_c5;
            float s = Math.sin(angle) * 0.5f;

            if (x < 0.5f)
            {
                return -Math.exp(t * ln_2) * s;
            }

            return (Math.exp(-t * ln_2) * s) + 1.0f;
        }


        /*  Bounce  ================================================================ */
        private static float BounceOut_Impl(float x)
        {
            const float n1 = 7.5625f;
            const float d1_inv = 1.0f / 2.75f;
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
        public static float BounceIn(float x) => 1.0f - BounceOut_Impl(1.0f - x);
        public static float BounceOut(float x) => BounceOut_Impl(x);
        public static float BounceInOut(float x)
        {
            float t = 2.0f * x;
            return x < 0.5f ? (1.0f - BounceOut_Impl(1.0f - t)) * 0.5f : (1.0f + BounceOut_Impl(t - 1.0f)) * 0.5f;
        }
    }
}
