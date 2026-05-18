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

    public static BindingModel LinuxBeforeNeutral()
    {
        var model = TwoViewsNeutralPlusLinux();
        return new BindingModel([model.Views[1], model.Views[0]]);
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

    public static BindingModel ModelWithStructs()
    {
        return new BindingModel(
            Views: [],
            Structs:
            [
                new BindingStruct(
                    Name: "SDL_CustomBytes",
                    Fields:
                    [
                        new BindingStructField("data", BindingTypeRef.Of("byte"), FieldOffset: null, FixedBufferLength: 16),
                    ],
                    Layout: System.Runtime.InteropServices.LayoutKind.Sequential,
                    ExplicitSize: null),
                new BindingStruct(
                    Name: "SDL_GameControllerButtonBind",
                    Fields:
                    [
                        new BindingStructField("bindType", BindingTypeRef.Of("int"), FieldOffset: null),
                        new BindingStructField("@value", BindingTypeRef.Of("SDL_GameControllerButtonBind_value"), FieldOffset: null),
                    ],
                    Layout: System.Runtime.InteropServices.LayoutKind.Sequential,
                    ExplicitSize: null),
                new BindingStruct(
                    Name: "SDL_GameControllerButtonBind_value",
                    Fields:
                    [
                        new BindingStructField("button", BindingTypeRef.Of("int"), FieldOffset: 0),
                        new BindingStructField("axis", BindingTypeRef.Of("int"), FieldOffset: 0),
                    ],
                    Layout: System.Runtime.InteropServices.LayoutKind.Explicit,
                    ExplicitSize: 8),
            ],
            Enums: [],
            Constants: [],
            Handles: [],
            Callbacks: []);
    }
}
