using Build.Targets.GenerateBindings.Model;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emitting;

internal static class PreviewBindingModelData
{
    public static PreviewBindingModel TwoViewsNeutralPlusLinux()
    {
        var neutral = new PreviewParseView(
            Name: "Neutral",
            SupportedOsPlatform: null,
            Functions:
            [
                new PreviewFunction(
                    "SDL_Init",
                    "int",
                    [new PreviewParameter("uint", "flags")],
                    "SDL_main.h"),
                new PreviewFunction(
                    "SDL_Quit",
                    "void",
                    [],
                    "SDL_main.h"),
            ]);

        var linux = new PreviewParseView(
            Name: "Linux",
            SupportedOsPlatform: "linux",
            Functions:
            [
                new PreviewFunction(
                    "SDL_LinuxSetThreadPriority",
                    "int",
                    [
                        new PreviewParameter("long", "threadID"),
                        new PreviewParameter("int", "priority"),
                    ],
                    "SDL_system.h"),
            ]);

        return new PreviewBindingModel([neutral, linux]);
    }

    public static PreviewBindingModel SingleNeutralEmptyParameterFunction()
    {
        return new PreviewBindingModel(
        [
            new PreviewParseView(
                Name: "Neutral",
                SupportedOsPlatform: null,
                Functions:
                [
                    new PreviewFunction("SDL_GetTicks", "uint", [], "SDL_timer.h"),
                ]),
        ]);
    }

    public static PreviewBindingModel EmptyModel()
        => new(new List<PreviewParseView>());
}
