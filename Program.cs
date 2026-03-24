using System;
using System.Collections.Generic;

namespace SatorImaging.UnityFundamentals
{
    public struct EasingFloat
    {
        private static float Sin5(float t)
        {
            const float inv6 = 1.0f / 6.0f;
            const float invC = 1.0f / 127.2236176f;
            return t - (t * t * t * inv6) + (t * t * t * t * t * invC);
        }
        public static float SineIn(float x) => 1.0f - Sin5((1.0f - x) * (float)Math.PI * 0.5f);
        public static float SineOut(float x) => Sin5(x * (float)Math.PI * 0.5f);
        public static float SineInOut(float x) => (1.0f - Sin5((0.5f - x) * (float)Math.PI)) * 0.5f;

        private static float FastSqrt(float x)
        {
            float xHalf = x * 0.5f;
            int i = BitConverter.SingleToInt32Bits(x);
            i = 0x5f3700a0 - (i >> 1);
            float y = BitConverter.Int32BitsToSingle(i);
            y *= 1.501898050f - (xHalf * y * y);
            return x <= 0.0f ? 0.0f : x * y;
        }
        public static float CircIn(float x) => 1.0f - FastSqrt(1.0f - (x * x));
        public static float CircOut(float x) => FastSqrt(1.0f - ((x - 1.0f) * (x - 1.0f)));
        public static float CircInOut(float x) => x < 0.5f ? (1.0f - FastSqrt(1.0f - ((2.0f * x) * (2.0f * x)))) * 0.5f : (FastSqrt(1.0f - ((2.0f - (2.0f * x)) * (2.0f - (2.0f * x)))) + 1.0f) * 0.5f;
    }

    public struct EasingDouble
    {
        private static double Sin5(double t)
        {
            const double inv6 = 1.0 / 6.0;
            const double invC = 0.007860176264317486;
            return t - (t * t * t * inv6) + (t * t * t * t * t * invC);
        }
        public static double SineIn(double x) => 1.0 - Sin5((1.0 - x) * Math.PI * 0.5);
        public static double SineOut(double x) => Sin5(x * Math.PI * 0.5);
        public static double SineInOut(double x) => (1.0 - Sin5((0.5 - x) * Math.PI)) * 0.5;

        private static double FastSqrt(double x)
        {
            double xHalf = x * 0.5;
            long i = BitConverter.DoubleToInt64Bits(x);
            i = 0x5fe6eb50c7b537a9L - (i >> 1);
            double y = BitConverter.Int64BitsToDouble(i);
            y *= 1.501750997142437871 - (xHalf * y * y);
            return x <= 0.0 ? 0.0 : x * y;
        }
        public static double CircIn(double x) => 1.0 - FastSqrt(1.0 - (x * x));
        public static double CircOut(double x) => FastSqrt(1.0 - ((x - 1.0) * (x - 1.0)));
        public static double CircInOut(double x) => x < 0.5 ? (1.0 - FastSqrt(1.0 - ((2.0 * x) * (2.0 * x)))) * 0.5 : (FastSqrt(1.0 - ((2.0 - (2.0 * x)) * (2.0 - (2.0 * x)))) + 1.0) * 0.5;
    }

    public class Program
    {
        static readonly Dictionary<string, float[]> ExpectedValues = new Dictionary<string, float[]>
        {
            { "CircIn", new float[] { 0.0000000f, 0.0049986f, 0.0201951f, 0.0453235f, 0.0818149f, 0.1330434f, 0.1998041f, 0.2847734f, 0.3995216f, 0.5635874f, 1.0000000f } },
            { "CircInOut", new float[] { 0.0000000f, 0.0100975f, 0.0409075f, 0.0999021f, 0.1997608f, 0.5000000f, 0.8002393f, 0.9000980f, 0.9590925f, 0.9899025f, 1.0000000f } },
            { "CircOut", new float[] { 0.0000000f, 0.4364126f, 0.6004785f, 0.7152266f, 0.8001959f, 0.8669566f, 0.9181851f, 0.9546765f, 0.9798049f, 0.9950014f, 1.0000000f } },
            { "SineIn", new float[] { 0.0000000f, 0.0128053f, 0.0494656f, 0.1093748f, 0.1912054f, 0.2929983f, 0.4122535f, 0.5460194f, 0.6909844f, 0.8435656f, 1.0000000f } },
            { "SineInOut", new float[] { 0.0000000f, 0.0247328f, 0.0956027f, 0.2061267f, 0.3454922f, 0.5000000f, 0.6545079f, 0.7938733f, 0.9043974f, 0.9752672f, 1.0000000f } },
            { "SineOut", new float[] { 0.0000000f, 0.1564344f, 0.3090156f, 0.4539806f, 0.5877466f, 0.7070017f, 0.8087946f, 0.8906252f, 0.9505344f, 0.9871947f, 1.0000000f } },
        };

        public static void Main()
        {
            Test("EasingFloat", x => (float)EasingFloat.SineIn((float)x), "SineIn");
            Test("EasingFloat", x => (float)EasingFloat.SineOut((float)x), "SineOut");
            Test("EasingFloat", x => (float)EasingFloat.SineInOut((float)x), "SineInOut");
            Test("EasingFloat", x => (float)EasingFloat.CircIn((float)x), "CircIn");
            Test("EasingFloat", x => (float)EasingFloat.CircOut((float)x), "CircOut");
            Test("EasingFloat", x => (float)EasingFloat.CircInOut((float)x), "CircInOut");

            Test("EasingDouble", x => (float)EasingDouble.SineIn(x), "SineIn");
            Test("EasingDouble", x => (float)EasingDouble.SineOut(x), "SineOut");
            Test("EasingDouble", x => (float)EasingDouble.SineInOut(x), "SineInOut");
            Test("EasingDouble", x => (float)EasingDouble.CircIn(x), "CircIn");
            Test("EasingDouble", x => (float)EasingDouble.CircOut(x), "CircOut");
            Test("EasingDouble", x => (float)EasingDouble.CircInOut(x), "CircInOut");
        }

        static void Test(string type, Func<double, float> func, string name)
        {
            float[] expected = ExpectedValues[name];
            float tolerance = name.StartsWith("Circ") ? 0.0002f : 0.0001f;
            for (int i = 0; i <= 10; i++)
            {
                double t = i * 0.1;
                float result = func(t);
                if (Math.Abs(result - expected[i]) > tolerance)
                {
                    Console.WriteLine($"[FAIL] {type}.{name}({t:F1}) expected {expected[i]:F7}, got {result:F7}, diff {Math.Abs(result - expected[i]):F7}, tolerance {tolerance}");
                }
            }
        }
    }
}
