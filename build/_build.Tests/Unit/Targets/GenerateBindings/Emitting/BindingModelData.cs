using Build.Targets.GenerateBindings.Model;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emitting;

internal static class BindingModelData
{
    public static BindingModel TwoViewsNeutralPlusLinux()
    {
        var neutral = new BindingParseView(
            Name: "Neutral",
            SupportedOsPlatform: null,
            Functions:
            [
                new BindingFunction(
                    "SDL_Init",
                    BindingTypeRef.Of("int"),
                    [new BindingParameter(BindingTypeRef.Of("uint"), "flags")],
                    "SDL.h"),
                new BindingFunction(
                    "SDL_Quit",
                    BindingTypeRef.Of("void"),
                    [],
                    "SDL.h"),
            ]);

        var linux = new BindingParseView(
            Name: "Linux",
            SupportedOsPlatform: "linux",
            Functions:
            [
                new BindingFunction(
                    "SDL_LinuxSetThreadPriority",
                    BindingTypeRef.Of("int"),
                    [
                        new BindingParameter(BindingTypeRef.Of("long"), "threadID"),
                        new BindingParameter(BindingTypeRef.Of("int"), "priority"),
                    ],
                    "SDL_system.h"),
            ]);

        return new BindingModel([neutral, linux]);
    }

    public static BindingModel SingleNeutralEmptyParameterFunction()
    {
        return new BindingModel(
        [
            new BindingParseView(
                Name: "Neutral",
                SupportedOsPlatform: null,
                Functions:
                [
                    new BindingFunction("SDL_GetTicks", BindingTypeRef.Of("uint"), [], "SDL_timer.h"),
                ]),
        ]);
    }

    public static BindingModel EmptyModel()
        => new(new List<BindingParseView>());
}
