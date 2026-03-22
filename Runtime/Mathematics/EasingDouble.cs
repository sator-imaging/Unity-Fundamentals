// High Performance Easing Functions for .NET / Unity
// (c) 2026 Sator Imaging, Licensed under the MIT License
// https://github.com/sator-imaging/Unity-Fundamentals

// Unlike to Unity's `Mathf`, `Math` doesn't have `LerpUnclamped` method.
// --> Workaround: double value = from + ((to - from) * t);

// Checklist:
// - No unnecessary division ops?
// - No float number literals?
// - To find non floating point double literals
//   --> (?<!\.)\b\d+\b(?!\.)

using System;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    public static class EasingDouble
    {
        /*  Quad  ================================================================ */
        /// <summary><c>=> pow(x, 2)</c></summary>
        public static double QuadIn(double x) => x * x;
        public static double QuadOut(double x)
        {
            double t = 1.0 - x;
            return 1.0 - (t * t);
        }
        public static double QuadInOut(double x)
        {
            if (x < 0.5)
            {
                return 2.0 * x * x;
            }

            double t = (-2.0 * x) + 2.0;
            return 1.0 - (t * t * 0.5);
        }


        /*  Cubic  ================================================================ */
        /// <summary><c>=> pow(x, 3)</c></summary>
        public static double CubicIn(double x) => x * x * x;
        public static double CubicOut(double x)
        {
            double t = 1.0 - x;
            return 1.0 - (t * t * t);
        }
        public static double CubicInOut(double x)
        {
            if (x < 0.5)
            {
                return 4.0 * x * x * x;
            }

            double t = (-2.0 * x) + 2.0;
            return 1.0 - (t * t * t * 0.5);
        }


        /*  Quart  ================================================================ */
        /// <summary><c>=> pow(x, 4)</c></summary>
        public static double QuartIn(double x) => x * x * x * x;
        public static double QuartOut(double x)
        {
            double t = 1.0 - x;
            return 1.0 - (t * t * t * t);
        }
        public static double QuartInOut(double x)
        {
            if (x < 0.5)
            {
                return 8.0 * x * x * x * x;
            }

            double t = (-2.0 * x) + 2.0;
            double t2 = t * t;
            return 1.0 - (t2 * t2 * 0.5);
        }


        /*  Quint  ================================================================ */
        /// <summary><c>=> pow(x, 5)</c></summary>
        public static double QuintIn(double x) => x * x * x * x * x;
        public static double QuintOut(double x)
        {
            double t = 1.0 - x;
            return 1.0 - (t * t * t * t * t);
        }
        public static double QuintInOut(double x)
        {
            if (x < 0.5)
            {
                return 16.0 * x * x * x * x * x;
            }

            double t = (-2.0 * x) + 2.0;
            double t2 = t * t;
            return 1.0 - (t2 * t2 * t * 0.5);
        }


        /*  Sine  ================================================================ */
        // x5 faster but low quality especially for Elastic (in slo mo, less motion continuity)
        private static double Sin5(double t)
        {
            const double inv6 = 1.0 / 6.0;
            const double inv120 = 1.0 / 120.0;

            // sin(t) ≈ t - t^3/6 + t^5/120
            double t2 = t * t;
            double t3 = t2 * t;
            double t5 = t3 * t2;
            return t - (t3 * inv6) + (t5 * inv120);
        }

        /// <summary>Uses approximate sine value.</summary>
        public static double SineIn(double x) => 1.0 - Sin5((1.0 - x) * Math.PI * 0.5);
        public static double SineOut(double x) => Sin5(x * Math.PI * 0.5);
        public static double SineInOut(double x) => (1.0 - Sin5((0.5 - x) * Math.PI)) * 0.5;


        /*  Expo  ================================================================ */
        private const double ln_2 = 0.6931471805599453;

        /// <summary><c>=> pow(2.0, 10.0 * x - 10.0)</c></summary>
        public static double ExpoIn(double x) => x == 0.0 ? 0.0 : Math.Exp(((10.0 * x) - 10.0) * ln_2);
        public static double ExpoOut(double x) => x == 1.0 ? 1.0 : 1.0 - Math.Exp((-10.0 * x) * ln_2);
        public static double ExpoInOut(double x)
        {
            if (x == 0.0)
            {
                return 0.0;
            }
            if (x == 1.0)
            {
                return 1.0;
            }

            double t = (20.0 * x) - 10.0;
            return x < 0.5
                ? Math.Exp(t * ln_2) * 0.5
                : (2.0 - Math.Exp((-t) * ln_2)) * 0.5;
        }


        /*  Circ  ================================================================ */
        private static double FastSqrt(double x)
        {
            if (x <= 0.0)
            {
                return 0.0;
            }

            double xHalf = x * 0.5;
            long i = BitConverter.DoubleToInt64Bits(x);
            i = 0x5fe6eb50c7b537a9L - (i >> 1);

            double y = BitConverter.Int64BitsToDouble(i);
            y *= 1.5 - (xHalf * y * y);
            return x * y;
        }

        /// <summary><c>=> 1.0 - sqrt(1.0 - x * x)</c></summary>
        public static double CircIn(double x) => 1.0 - FastSqrt(1.0 - (x * x));
        public static double CircOut(double x)
        {
            double t = x - 1.0;
            return FastSqrt(1.0 - (t * t));
        }
        public static double CircInOut(double x)
        {
            double t = 2.0 * x;
            if (x < 0.5)
            {
                return (1.0 - FastSqrt(1.0 - (t * t))) * 0.5;
            }

            double u = 2.0 - t;
            return (FastSqrt(1.0 - (u * u)) + 1.0) * 0.5;
        }


        /*  Back  ================================================================ */
        private const double c1 = 1.70158;
        private const double c2 = c1 * 1.525;

        /// <summary>Overshoots the range once and returns.</summary>
        public static double BackIn(double x) => ((c1 + 1.0) * x * x * x) - (c1 * x * x);
        public static double BackOut(double x)
        {
            double t = x - 1.0;
            return 1.0 + ((c1 + 1.0) * t * t * t) + (c1 * t * t);
        }
        public static double BackInOut(double x)
        {
            double t = 2.0 * x;
            if (x < 0.5)
            {
                return (t * t * (((c2 + 1.0) * t) - c2)) * 0.5;
            }

            double u = t - 2.0;
            return ((u * u * (((c2 + 1.0) * u) + c2)) + 2.0) * 0.5;
        }


        /*  Elastic  ================================================================ */
        private const double e_c4 = 2.0 * Math.PI / 3.0;
        private const double e_c5 = 2.0 * Math.PI / 4.5;

        /// <summary>Simulates spring-like oscillations.</summary>
        public static double ElasticIn(double x)
        {
            if (x == 0.0)
            {
                return 0.0;
            }

            if (x == 1.0)
            {
                return 1.0;
            }

            double t = 10.0 * x;
            return -Math.Exp((t - 10.0) * ln_2) * Math.Sin((t - 10.75) * e_c4);
        }

        public static double ElasticOut(double x)
        {
            if (x == 0.0)
            {
                return 0.0;
            }

            if (x == 1.0)
            {
                return 1.0;
            }

            double t = 10.0 * x;
            return (Math.Exp((-t) * ln_2) * Math.Sin((t - 0.75) * e_c4)) + 1.0;
        }

        public static double ElasticInOut(double x)
        {
            if (x == 0.0)
            {
                return 0.0;
            }

            if (x == 1.0)
            {
                return 1.0;
            }

            double t = (20.0 * x) - 10.0;
            double angle = (t - 1.125) * e_c5;
            double s = Math.Sin(angle) * 0.5;

            if (x < 0.5)
            {
                return -Math.Exp(t * ln_2) * s;
            }

            return (Math.Exp(-t * ln_2) * s) + 1.0;
        }


        /*  Bounce  ================================================================ */
        private static double BounceOut_Impl(double x)
        {
            const double n1 = 7.5625;
            const double d1_inv = 1.0 / 2.75;
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
        public static double BounceIn(double x) => 1.0 - BounceOut_Impl(1.0 - x);
        public static double BounceOut(double x) => BounceOut_Impl(x);
        public static double BounceInOut(double x)
        {
            double t = 2.0 * x;
            return x < 0.5 ? (1.0 - BounceOut_Impl(1.0 - t)) * 0.5 : (1.0 + BounceOut_Impl(t - 1.0)) * 0.5;
        }
    }
}
