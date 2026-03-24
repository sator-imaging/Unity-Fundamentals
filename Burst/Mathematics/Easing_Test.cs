using System;
using System.Collections.Generic;
using System.Reflection;

namespace SatorImaging.UnityFundamentals
{
    public static class Easing_Test
    {
        public static void Run()
        {
            TestType(typeof(EasingFloat), typeof(float));
            TestType(typeof(EasingDouble), typeof(double));
        }

        private static void TestType(Type type, Type scalarType)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (method.ReturnType != scalarType || parameters.Length != 1 || parameters[0].ParameterType != scalarType)
                    continue;

                if (!ExpectedValues.TryGetValue(method.Name, out float[] expected))
                {
                    continue;
                }

                for (int i = 0; i <= 10; i++)
                {
                    float t = i * 0.1f;
                    object tObj = scalarType == typeof(float) ? (object)t : (object)(double)t;
                    object resultObj = method.Invoke(null, new object[] { tObj });
                    float result = scalarType == typeof(float) ? (float)resultObj : (float)(double)resultObj;

                    if (Math.Abs(result - expected[i]) > 0.0001f)
                    {
                        throw new Exception($"Easing Test Failed: {type.Name}.{method.Name}({t}) expected {expected[i]}, got {result}");
                    }
                }
            }
        }

        private static readonly Dictionary<string, float[]> ExpectedValues = new Dictionary<string, float[]>
        {
            { "BackIn", new float[] { 0.0000000f, -0.0143142f, -0.0464506f, -0.0801995f, -0.0993517f, -0.0876975f, -0.0290275f, 0.0928677f, 0.2941978f, 0.5911720f, 1.0000000f } },
            { "BackInOut", new float[] { -0.0000000f, -0.0375186f, -0.0925557f, -0.0788335f, 0.0899258f, 0.5000000f, 0.9100742f, 1.0788335f, 1.0925557f, 1.0375186f, 1.0000000f } },
            { "BackOut", new float[] { 0.0000000f, 0.4088280f, 0.7058022f, 0.9071323f, 1.0290275f, 1.0876975f, 1.0993517f, 1.0801995f, 1.0464506f, 1.0143142f, 1.0000000f } },
            { "BounceIn", new float[] { 0.0000000f, 0.0118750f, 0.0600000f, 0.0693750f, 0.2275000f, 0.2343750f, 0.0900000f, 0.3193750f, 0.6975000f, 0.9243750f, 1.0000000f } },
            { "BounceInOut", new float[] { 0.0000000f, 0.0300000f, 0.1137500f, 0.0450000f, 0.3487500f, 0.5000000f, 0.6512500f, 0.9550000f, 0.8862500f, 0.9700000f, 1.0000000f } },
            { "BounceOut", new float[] { 0.0000000f, 0.0756250f, 0.3025000f, 0.6806250f, 0.9100000f, 0.7656250f, 0.7725000f, 0.9306250f, 0.9400000f, 0.9881250f, 1.0000000f } },
            { "CircIn", new float[] { 0.0017615f, 0.0067516f, 0.0219213f, 0.0470187f, 0.0834870f, 0.1346595f, 0.2013116f, 0.2860957f, 0.4006454f, 0.5643993f, 1.0000000f } },
            { "CircInOut", new float[] { 0.0008808f, 0.0109606f, 0.0417435f, 0.1006558f, 0.2003227f, 0.5000000f, 0.7996773f, 0.8993442f, 0.9582565f, 0.9890394f, 0.9991192f } },
            { "CircOut", new float[] { 0.0000000f, 0.4356007f, 0.5993546f, 0.7139043f, 0.7986884f, 0.8653405f, 0.9165130f, 0.9529813f, 0.9780787f, 0.9932484f, 0.9982385f } },
            { "CubicIn", new float[] { 0.0000000f, 0.0010000f, 0.0080000f, 0.0270000f, 0.0640000f, 0.1250000f, 0.2160000f, 0.3430000f, 0.5120000f, 0.7290000f, 1.0000000f } },
            { "CubicInOut", new float[] { 0.0000000f, 0.0040000f, 0.0320000f, 0.1080000f, 0.2560000f, 0.5000000f, 0.7440000f, 0.8920000f, 0.9680000f, 0.9960000f, 1.0000000f } },
            { "CubicOut", new float[] { 0.0000000f, 0.2710000f, 0.4880000f, 0.6570000f, 0.7840000f, 0.8750000f, 0.9360000f, 0.9730000f, 0.9920000f, 0.9990000f, 1.0000000f } },
            { "ElasticIn", new float[] { 0.0000000f, 0.0019531f, -0.0019531f, -0.0039062f, 0.0156250f, -0.0156250f, -0.0312500f, 0.1250000f, -0.1250000f, -0.2500000f, 1.0000000f } },
            { "ElasticInOut", new float[] { 0.0000000f, 0.0003392f, -0.0039062f, 0.0239389f, -0.1174616f, 0.5000000f, 1.1174616f, 0.9760611f, 1.0039062f, 0.9996608f, 1.0000000f } },
            { "ElasticOut", new float[] { 0.0000000f, 1.2500000f, 1.1250000f, 0.8750000f, 1.0312500f, 1.0156250f, 0.9843750f, 1.0039062f, 1.0019531f, 0.9980469f, 1.0000000f } },
            { "ExpoIn", new float[] { 0.0000000f, 0.0019531f, 0.0039063f, 0.0078125f, 0.0156250f, 0.0312500f, 0.0625000f, 0.1250000f, 0.2500000f, 0.5000000f, 1.0000000f } },
            { "ExpoInOut", new float[] { 0.0000000f, 0.0019531f, 0.0078125f, 0.0312500f, 0.1250000f, 0.5000000f, 0.8750000f, 0.9687500f, 0.9921875f, 0.9980469f, 1.0000000f } },
            { "ExpoOut", new float[] { 0.0000000f, 0.5000000f, 0.7500000f, 0.8750000f, 0.9375000f, 0.9687500f, 0.9843750f, 0.9921875f, 0.9960938f, 0.9980469f, 1.0000000f } },
            { "QuadIn", new float[] { 0.0000000f, 0.0100000f, 0.0400000f, 0.0900000f, 0.1600000f, 0.2500000f, 0.3600000f, 0.4900000f, 0.6400000f, 0.8100000f, 1.0000000f } },
            { "QuadInOut", new float[] { 0.0000000f, 0.0200000f, 0.0800000f, 0.1800000f, 0.3200000f, 0.5000000f, 0.6800000f, 0.8200000f, 0.9200000f, 0.9800000f, 1.0000000f } },
            { "QuadOut", new float[] { 0.0000000f, 0.1900000f, 0.3600000f, 0.5100000f, 0.6400000f, 0.7500000f, 0.8400000f, 0.9100000f, 0.9600000f, 0.9900000f, 1.0000000f } },
            { "QuartIn", new float[] { 0.0000000f, 0.0001000f, 0.0016000f, 0.0081000f, 0.0256000f, 0.0625000f, 0.1296000f, 0.2401000f, 0.4096000f, 0.6561000f, 1.0000000f } },
            { "QuartInOut", new float[] { 0.0000000f, 0.0008000f, 0.0128000f, 0.0648000f, 0.2048000f, 0.5000000f, 0.7952000f, 0.9352000f, 0.9872000f, 0.9992000f, 1.0000000f } },
            { "QuartOut", new float[] { 0.0000000f, 0.3439000f, 0.5904000f, 0.7599000f, 0.8704000f, 0.9375000f, 0.9744000f, 0.9919000f, 0.9984000f, 0.9999000f, 1.0000000f } },
            { "QuintIn", new float[] { 0.0000000f, 0.0000100f, 0.0003200f, 0.0024300f, 0.0102400f, 0.0312500f, 0.0777600f, 0.1680700f, 0.3276800f, 0.5904900f, 1.0000000f } },
            { "QuintInOut", new float[] { 0.0000000f, 0.0001600f, 0.0051200f, 0.0388800f, 0.1638400f, 0.5000000f, 0.8361600f, 0.9611200f, 0.9948800f, 0.9998400f, 1.0000000f } },
            { "QuintOut", new float[] { 0.0000000f, 0.4095100f, 0.6723200f, 0.8319300f, 0.9222400f, 0.9687500f, 0.9897600f, 0.9975700f, 0.9996800f, 0.9999900f, 1.0000000f } },
            { "SineIn", new float[] { -0.0045249f, 0.0101334f, 0.0479829f, 0.1086143f, 0.1908535f, 0.2928569f, 0.4122071f, 0.5460085f, 0.6909829f, 0.8435655f, 1.0000000f } },
            { "SineInOut", new float[] { -0.0022624f, 0.0239914f, 0.0954268f, 0.2061036f, 0.3454915f, 0.5000000f, 0.6545085f, 0.7938964f, 0.9045732f, 0.9760086f, 1.0022624f } },
            { "SineOut", new float[] { 0.0000000f, 0.1564345f, 0.3090171f, 0.4539915f, 0.5877929f, 0.7071431f, 0.8091465f, 0.8913857f, 0.9520171f, 0.9898666f, 1.0045249f } },
        };
    }
}
