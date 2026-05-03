using System;
using System.Collections.Generic;
using System.Linq;
using SatorImaging.UnityFundamentals;

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

    private static double Calculate(string name, double x)
    {
        switch (name)
        {
            case "QuadIn": return EasingDouble.QuadIn_Raw(x);
            case "QuadOut": return EasingDouble.QuadOut_Raw(x);
            case "QuadInOut": return EasingDouble.QuadInOut_Raw(x);

            case "CubicIn": return EasingDouble.CubicIn_Raw(x);
            case "CubicOut": return EasingDouble.CubicOut_Raw(x);
            case "CubicInOut": return EasingDouble.CubicInOut_Raw(x);

            case "QuartIn": return EasingDouble.QuartIn_Raw(x);
            case "QuartOut": return EasingDouble.QuartOut_Raw(x);
            case "QuartInOut": return EasingDouble.QuartInOut_Raw(x);

            case "QuintIn": return EasingDouble.QuintIn_Raw(x);
            case "QuintOut": return EasingDouble.QuintOut_Raw(x);
            case "QuintInOut": return EasingDouble.QuintInOut_Raw(x);

            case "SineIn": return EasingDouble.SineIn_Raw(x);
            case "SineOut": return EasingDouble.SineOut_Raw(x);
            case "SineInOut": return EasingDouble.SineInOut_Raw(x);

            case "ExpoIn": return EasingDouble.ExpoIn_Raw(x);
            case "ExpoOut": return EasingDouble.ExpoOut_Raw(x);
            case "ExpoInOut": return EasingDouble.ExpoInOut_Raw(x);

            case "CircIn": return EasingDouble.CircIn_Raw(x);
            case "CircOut": return EasingDouble.CircOut_Raw(x);
            case "CircInOut": return EasingDouble.CircInOut_Raw(x);

            case "BackIn": return EasingDouble.BackIn_Raw(x);
            case "BackOut": return EasingDouble.BackOut_Raw(x);
            case "BackInOut": return EasingDouble.BackInOut_Raw(x);

            case "ElasticIn": return EasingDouble.ElasticIn_Raw(x);
            case "ElasticOut": return EasingDouble.ElasticOut_Raw(x);
            case "ElasticInOut": return EasingDouble.ElasticInOut_Raw(x);

            case "BounceIn": return EasingDouble.BounceIn_Raw(x);
            case "BounceOut": return EasingDouble.BounceOut_Raw(x);
            case "BounceInOut": return EasingDouble.BounceInOut_Raw(x);

            default: throw new Exception("Unknown");
        }
    }
}
