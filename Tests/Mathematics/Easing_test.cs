#:package FUnit@*
#:package FUnit.Directives@*

//:funit:include ../../Burst/Mathematics/EasingFloat.cs
//:funit:include ../../Burst/Mathematics/EasingDouble.cs
//:funit:include ../../Burst/Mathematics/Easing_Test.cs

using System.Linq;
using SatorImaging.UnityFundamentals;

#if !UNITY_5_3_OR_NEWER
namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = (float)System.Math.PI;
        public static float Exp(float x) => (float)System.Math.Exp(x);
        public static float Sin(float x) => (float)System.Math.Sin(x);
        public static float Cos(float x) => (float)System.Math.Cos(x);
        public static float Sqrt(float x) => (float)System.Math.Sqrt(x);
        public static float Pow(float x, float y) => (float)System.Math.Pow(x, y);
    }
}
#endif

return FUnit.Run(args, describe =>
{
    describe("Easing functions validation", it =>
    {
        it("should pass all easing tests (expect 30 for float and 30 for double)", () =>
        {
            var result = Easing_Test.Run();
            var lines = result.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);

            var floatPasses = lines.Count(l => l.Contains("[Pass] EasingFloat."));
            var doublePasses = lines.Count(l => l.Contains("[Pass] EasingDouble."));

            Must.BeEqual(30, floatPasses);
            Must.BeEqual(30, doublePasses);
        });
    });
});
