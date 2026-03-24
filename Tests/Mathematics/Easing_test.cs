#:package FUnit@*
#:package FUnit.Directives@*

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
