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
                    "int",
                    [new BindingParameter("uint", "flags")],
                    "SDL.h"),
                new BindingFunction(
                    "SDL_Quit",
                    "void",
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
                    "int",
                    [
                        new BindingParameter("long", "threadID"),
                        new BindingParameter("int", "priority"),
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
                    new BindingFunction("SDL_GetTicks", "uint", [], "SDL_timer.h"),
                ]),
        ]);
    }

    public static BindingModel EmptyModel()
        => new(new List<BindingParseView>());
}
