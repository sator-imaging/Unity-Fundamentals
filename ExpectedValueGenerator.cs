using System;
using System.Collections.Generic;
using System.Linq;

public class ExpectedValueGenerator
{
    public static void Main()
    {
        var names = new string[]
        {
            "BackIn", "BackInOut", "BackOut", "BounceIn", "BounceInOut", "BounceOut",
            "CircIn", "CircInOut", "CircOut", "CubicIn", "CubicInOut", "CubicOut",
            "ElasticIn", "ElasticInOut", "ElasticOut", "ExpoIn", "ExpoInOut", "ExpoOut",
            "QuadIn", "QuadInOut", "QuadOut", "QuartIn", "QuartInOut", "QuartOut",
            "QuintIn", "QuintInOut", "QuintOut", "SineIn", "SineInOut", "SineOut"
        };

        Console.WriteLine("        private static readonly Dictionary<string, double[]> ExpectedValues = new Dictionary<string, double[]>");
        Console.WriteLine("        {");

        foreach (var name in names.OrderBy(n => n))
        {
            Console.Write($"            {{ \"{name}\", new double[] {{ ");
            for (int i = 0; i <= 10; i++)
            {
                double x = Math.Round(i * 0.1, 1);
                double val = Calculate(name, x);
                Console.Write(val.ToString("G17"));
                if (i < 10) Console.Write(", ");
            }
            Console.WriteLine(" } },");
        }

        Console.WriteLine("        };");
    }

    private const double c1 = 1.70158;
    private const double c2 = c1 * 1.525;
    private const double ln_2 = 0.6931471805599453;
    private static readonly double e_c4 = 2.0 * Math.PI / 3.0;
    private static readonly double e_c5 = 2.0 * Math.PI / 4.5;

    // Use approximate functions as defined in EasingDouble.cs to see actual behavior before guards
    private static double Sin5(double t)
    {
        const double inv6 = 1.0 / 6.0;
        const double invC = 0.007860176264317486;
        return t - (t * t * t * inv6) + (t * t * t * t * t * invC);
    }

    private static double FastSqrt(double x)
    {
        double xHalf = x * 0.5;
        long i = BitConverter.DoubleToInt64Bits(x);
        i = 0x5fe6eb50c7b537a9L - (i >> 1);
        double y = BitConverter.Int64BitsToDouble(i);
        y *= 1.501750997142437871 - (xHalf * y * y);
        return x * y;
    }

    private static double BounceOut_Impl(double x)
    {
        const double n1 = 7.5625;
        const double d1_inv = 1.0 / 2.75;
        if (x < 1.0 * d1_inv) return n1 * x * x;
        else if (x < 2.0 * d1_inv) return n1 * (x - (1.5 * d1_inv)) * (x - (1.5 * d1_inv)) + 0.75;
        else if (x < 2.5 * d1_inv) return n1 * (x - (2.25 * d1_inv)) * (x - (2.25 * d1_inv)) + 0.9375;
        else return n1 * (x - (2.625 * d1_inv)) * (x - (2.625 * d1_inv)) + 0.984375;
    }

    private static double Calculate(string name, double x)
    {
        switch (name)
        {
            case "QuadIn": return x * x;
            case "QuadOut": return 1.0 - ((1.0 - x) * (1.0 - x));
            case "QuadInOut": return x < 0.5 ? 2.0 * x * x : 1.0 - (Math.Pow(-2.0 * x + 2.0, 2) * 0.5);

            case "CubicIn": return x * x * x;
            case "CubicOut": return 1.0 - ((1.0 - x) * (1.0 - x) * (1.0 - x));
            case "CubicInOut": return x < 0.5 ? 4.0 * x * x * x : 1.0 - (Math.Pow(-2.0 * x + 2.0, 3) * 0.5);

            case "QuartIn": return x * x * x * x;
            case "QuartOut": return 1.0 - ((1.0 - x) * (1.0 - x) * (1.0 - x) * (1.0 - x));
            case "QuartInOut": return x < 0.5 ? 8.0 * x * x * x * x : 1.0 - (Math.Pow(-2.0 * x + 2.0, 4) * 0.5);

            case "QuintIn": return x * x * x * x * x;
            case "QuintOut": return 1.0 - ((1.0 - x) * (1.0 - x) * (1.0 - x) * (1.0 - x) * (1.0 - x));
            case "QuintInOut": return x < 0.5 ? 16.0 * x * x * x * x * x : 1.0 - (Math.Pow(-2.0 * x + 2.0, 5) * 0.5);

            case "SineIn": return 1.0 - Sin5((1.0 - x) * Math.PI * 0.5);
            case "SineOut": return Sin5(x * Math.PI * 0.5);
            case "SineInOut": return (1.0 - Sin5((0.5 - x) * Math.PI)) * 0.5;

            case "ExpoIn": return Math.Exp(((10.0 * x) - 10.0) * ln_2);
            case "ExpoOut": return 1.0 - Math.Exp((-10.0 * x) * ln_2);
            case "ExpoInOut": return x < 0.5 ? Math.Exp(((20.0 * x) - 10.0) * ln_2) * 0.5 : (2.0 - Math.Exp((10.0 - (20.0 * x)) * ln_2)) * 0.5;

            case "CircIn": return 1.0 - FastSqrt(1.0 - (x * x));
            case "CircOut": return FastSqrt(1.0 - ((x - 1.0) * (x - 1.0)));
            case "CircInOut": return x < 0.5 ? (1.0 - FastSqrt(1.0 - ((2.0 * x) * (2.0 * x)))) * 0.5 : (FastSqrt(1.0 - ((2.0 - (2.0 * x)) * (2.0 - (2.0 * x)))) + 1.0) * 0.5;

            case "BackIn": return ((c1 + 1.0) * x * x * x) - (c1 * x * x);
            case "BackOut": return 1.0 + ((c1 + 1.0) * Math.Pow(x - 1.0, 3)) + (c1 * Math.Pow(x - 1.0, 2));
            case "BackInOut": return x < 0.5 ? ((2.0 * x) * (2.0 * x) * (((c2 + 1.0) * (2.0 * x)) - c2)) * 0.5 : (((2.0 * x - 2.0) * (2.0 * x - 2.0) * (((c2 + 1.0) * (2.0 * x - 2.0)) + c2)) + 2.0) * 0.5;

            case "ElasticIn": return -Math.Exp(((10.0 * x) - 10.0) * ln_2) * Math.Sin(((10.0 * x) - 10.75) * e_c4);
            case "ElasticOut": return (Math.Exp((-10.0 * x) * ln_2) * Math.Sin(((10.0 * x) - 0.75) * e_c4)) + 1.0;
            case "ElasticInOut": return x < 0.5 ? -Math.Exp(((20.0 * x) - 10.0) * ln_2) * Math.Sin(((20.0 * x) - 11.125) * e_c5) * 0.5 : (Math.Exp((10.0 - (20.0 * x)) * ln_2) * Math.Sin(((20.0 * x) - 11.125) * e_c5) * 0.5) + 1.0;

            case "BounceIn": return 1.0 - BounceOut_Impl(1.0 - x);
            case "BounceOut": return BounceOut_Impl(x);
            case "BounceInOut":
                double t = 2.0 * x;
                return x < 0.5 ? (1.0 - BounceOut_Impl(1.0 - t)) * 0.5 : (1.0 + BounceOut_Impl(t - 1.0)) * 0.5;

            default: throw new Exception("Unknown");
        }
    }
}
