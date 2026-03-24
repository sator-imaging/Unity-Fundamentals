#:package FUnit@*
#:package FUnit.Directives@*

namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = (float)System.Math.PI;
        public static float Exp(float x) => (float)System.Math.Exp(x);
        public static float Sin(float x) => (float)System.Math.Sin(x);
    }
}

//:funit:include ../../Burst/Mathematics/EasingFloat.cs
//:funit:include ../../Burst/Mathematics/EasingDouble.cs
//:funit:include ../../Burst/Mathematics/Easing_Test.cs

using SatorImaging.UnityFundamentals;

return FUnit.Run(args, describe =>
{
    describe("Easing functions validation", it =>
    {
        it("should pass all easing tests", () =>
        {
            var result = Easing_Test.Run();
            Must.Contain("[Pass]", result);
        });
    });
});
