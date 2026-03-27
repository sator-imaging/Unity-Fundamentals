using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SatorImaging.UnityFundamentals
{
    public static class Easing_Test
    {
        public static string Run()
        {
            return TestType(typeof(EasingFloat), typeof(float))
                + "\n"
                + TestType(typeof(EasingDouble), typeof(double));
        }

        private static string TestType(Type type, Type scalarType)
        {
            // Collect all public static methods matching the easing signature: PRECISION Func(PRECISION x)
            MethodInfo[] allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            var classMethodNames = new HashSet<string>();
            foreach (var method in allMethods)
            {
                var parameters = method.GetParameters();
                if (method.ReturnType == scalarType && parameters.Length == 1 && parameters[0].ParameterType == scalarType)
                {
                    classMethodNames.Add(method.Name);
                }
            }

            // Bidirectional Existence Check
            // 1. Ensure all methods in the class are present in ExpectedValues.
            foreach (var name in classMethodNames)
            {
                if (!ExpectedValues.ContainsKey(name))
                {
                    throw new Exception($"Easing Test Failed: Method {type.Name}.{name} exists in class but is missing from ExpectedValues dictionary.");
                }
            }

            // 2. Ensure all entries in ExpectedValues exist in the class.
            foreach (var pair in ExpectedValues)
            {
                if (!classMethodNames.Contains(pair.Key))
                {
                    throw new Exception($"Easing Test Failed: Entry '{pair.Key}' in ExpectedValues has no corresponding public method in {type.Name}.");
                }
            }

            var sb = new StringBuilder();

            // Value Validation
            foreach (var name in classMethodNames)
            {
                MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, new Type[] { scalarType }, null)!;
                double[] expected = ExpectedValues[name];
                for (int i = 0; i <= 10; i++)
                {
                    double t = Math.Round(i * 0.1, 1);
                    object tObj = scalarType == typeof(float) ? (object)(float)t : (object)t;
                    object resultObj = method.Invoke(null, new object[] { tObj });

                    double targetExpected = expected[i];
                    bool isApprox = name.StartsWith("Sine") || name.StartsWith("Circ");

                    if (scalarType == typeof(float))
                    {
                        float result = (float)resultObj;
                        float expectedF = (float)targetExpected;
                        float tolerance = (i == 0 || i == 10) ? 0f : (isApprox ? 0.005f : 0.0002f);

                        if (Math.Abs(result - expectedF) > tolerance)
                        {
                            throw new Exception($"Easing Test Failed: {type.Name}.{name}({t}) expected {expectedF}, got {result} (diff: {Math.Abs(result - expectedF)}, tolerance: {tolerance})");
                        }
                    }
                    else // double
                    {
                        double result = (double)resultObj;
                        double expectedD = targetExpected;
                        double tolerance = (i == 0 || i == 10) ? 0.0 : (isApprox ? 0.005 : 1e-12);

                        if (Math.Abs(result - expectedD) > tolerance)
                        {
                            throw new Exception($"Easing Test Failed: {type.Name}.{name}({t}) expected {expectedD}, got {result} (diff: {Math.Abs(result - expectedD)}, tolerance: {tolerance})");
                        }
                    }
                }
                sb.AppendLine($"[Pass] {type.Name}.{name}");
            }

            return sb.ToString().Trim();
        }

        private static readonly Dictionary<string, double[]> ExpectedValues = new Dictionary<string, double[]>
        {
            { "BackIn", new double[] { 0, -0.01431422, -0.04645056, -0.08019954, -0.09935168, -0.0876975, -0.02902752, 0.09286774, 0.29419776, 0.59117202, 1 } },
            { "BackInOut", new double[] { 0, -0.037518552, -0.092555656, -0.078833484, 0.089925792, 0.5, 0.910074208, 1.078833484, 1.092555656, 1.037518552, 1 } },
            { "BackOut", new double[] { 0, 0.40882798, 0.70580224, 0.90713226, 1.02902752, 1.0876975, 1.09935168, 1.08019954, 1.04645056, 1.01431422, 1 } },
            { "BounceIn", new double[] { 0, 0.011875, 0.06, 0.069375, 0.2275, 0.234375, 0.09, 0.319375, 0.6975, 0.924375, 1 } },
            { "BounceInOut", new double[] { 0, 0.03, 0.11375, 0.045, 0.34875, 0.5, 0.65125, 0.955, 0.88625, 0.97, 1 } },
            { "BounceOut", new double[] { 0, 0.075625, 0.3025, 0.680625, 0.91, 0.765625, 0.7725, 0.930625, 0.94, 0.988125, 1 } },
            { "CircIn", new double[] { 0, 0.0050125628933800348, 0.020204102886728803, 0.046060798583054341, 0.083484861008832012, 0.1339745962155614, 0.19999999999999996, 0.28585715714571502, 0.40000000000000013, 0.56411010564593278, 1 } },
            { "CircInOut", new double[] { 0, 0.010102051443364402, 0.041742430504416006, 0.099999999999999978, 0.20000000000000007, 0.5, 0.79999999999999993, 0.89999999999999991, 0.95825756949558405, 0.9898979485566356, 1 } },
            { "CircOut", new double[] { 0, 0.43588989435406728, 0.59999999999999987, 0.71414284285428498, 0.80000000000000004, 0.8660254037844386, 0.91651513899116799, 0.95393920141694566, 0.9797958971132712, 0.99498743710661997, 1 } },
            { "CubicIn", new double[] { 0, 0.001, 0.008, 0.027, 0.064, 0.125, 0.216, 0.343, 0.512, 0.729, 1 } },
            { "CubicInOut", new double[] { 0, 0.004, 0.032, 0.108, 0.256, 0.5, 0.744, 0.892, 0.968, 0.996, 1 } },
            { "CubicOut", new double[] { 0, 0.271, 0.488, 0.657, 0.784, 0.875, 0.936, 0.973, 0.992, 0.999, 1 } },
            { "ElasticIn", new double[] { 0, 0.001953125, -0.001953125, -0.00390625, 0.015625, -0.015625, -0.03125, 0.125, -0.125, -0.25, 1 } },
            { "ElasticInOut", new double[] { 0, 0.000339156597005722, -0.00390625, 0.023938888847468056, -0.11746157759823853, 0.5, 1.1174615775982386, 0.97606111115253191, 1.00390625, 0.99966084340299433, 1 } },
            { "ElasticOut", new double[] { 0, 1.25, 1.125, 0.875, 1.03125, 1.015625, 0.984375, 1.00390625, 1.001953125, 0.998046875, 1 } },
            { "ExpoIn", new double[] { 0, 0.001953125, 0.00390625, 0.0078125, 0.015625, 0.03125, 0.0625, 0.125, 0.25, 0.5, 1 } },
            { "ExpoInOut", new double[] { 0, 0.001953125, 0.0078125, 0.03125, 0.125, 0.5, 0.875, 0.96875, 0.9921875, 0.998046875, 1 } },
            { "ExpoOut", new double[] { 0, 0.5, 0.75, 0.875, 0.9375, 0.96875, 0.984375, 0.9921875, 0.99609375, 0.998046875, 1 } },
            { "QuadIn", new double[] { 0, 0.01, 0.04, 0.09, 0.16, 0.25, 0.36, 0.49, 0.64, 0.81, 1 } },
            { "QuadInOut", new double[] { 0, 0.02, 0.08, 0.18, 0.32, 0.5, 0.68, 0.82, 0.92, 0.98, 1 } },
            { "QuadOut", new double[] { 0, 0.19, 0.36, 0.51, 0.64, 0.75, 0.84, 0.91, 0.96, 0.99, 1 } },
            { "QuartIn", new double[] { 0, 0.0001, 0.0016, 0.0081, 0.0256, 0.0625, 0.1296, 0.2401, 0.4096, 0.6561, 1 } },
            { "QuartInOut", new double[] { 0, 0.0008, 0.0128, 0.0648, 0.2048, 0.5, 0.7952, 0.9352, 0.9872, 0.9992, 1 } },
            { "QuartOut", new double[] { 0, 0.3439, 0.5904, 0.7599, 0.8704, 0.9375, 0.9744, 0.9919, 0.9984, 0.9999, 1 } },
            { "QuintIn", new double[] { 0, 1.0000000000000003e-05, 0.00032000000000000008, 0.0024299999999999994, 0.01024, 0.03125, 0.07776, 0.16807, 0.32768, 0.59049, 1 } },
            { "QuintInOut", new double[] { 0, 0.00016, 0.00512, 0.03888, 0.16384, 0.5, 0.83616, 0.96112, 0.99488, 0.99984, 1 } },
            { "QuintOut", new double[] { 0, 0.40951, 0.67232, 0.83193, 0.92224, 0.96875, 0.98976, 0.99757, 0.99968, 0.99999, 1 } },
            { "SineIn", new double[] { 0, 0.01231165940486223, 0.048943483704846469, 0.1089934758116321, 0.19098300562505255, 0.29289321881345243, 0.41221474770752686, 0.54600950026045325, 0.69098300562505255, 0.84356553495976905, 1 } },
            { "SineInOut", new double[] { 0, 0.024471741852423234, 0.095491502812526274, 0.20610737385376343, 0.34549150281252627, 0.5, 0.65450849718747373, 0.79389262614623646, 0.90450849718747373, 0.97552825814757682, 1 } },
            { "SineOut", new double[] { 0, 0.15643446504023087, 0.3090169943749474, 0.45399049973954675, 0.58778525229247314, 0.70710678118654746, 0.80901699437494745, 0.89100652418836779, 0.95105651629515353, 0.98768834059513777, 1 } },
        };
    }
}
