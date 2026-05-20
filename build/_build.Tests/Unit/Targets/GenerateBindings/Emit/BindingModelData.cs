using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.PlatformViews;
using Build.Tests.Fixtures;
using System.Runtime.InteropServices;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emit;

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
                    BindingGenerationFixture.NativeInt(),
                    [new BindingParameter(BindingGenerationFixture.NativeUInt(), "flags")],
                    "SDL.h"),
                new BindingFunction(
                    "SDL_Quit",
                    BindingGenerationFixture.NativeVoid(),
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
                    BindingGenerationFixture.NativeInt(),
                    [
                        new BindingParameter(BindingGenerationFixture.NativePrimitive("long", "long"), "threadID"),
                        new BindingParameter(BindingGenerationFixture.NativeInt(), "priority"),
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
                    new BindingFunction("SDL_GetTicks", BindingGenerationFixture.NativeUInt(), [], "SDL_timer.h"),
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
                        new BindingStructField("data", BindingGenerationFixture.NativePrimitive("unsigned char", "byte"), FieldOffset: null, FixedBufferLength: 16),
                    ],
                    Layout: System.Runtime.InteropServices.LayoutKind.Sequential,
                    ExplicitSize: null),
                new BindingStruct(
                    Name: "SDL_GameControllerButtonBind",
                    Fields:
                    [
                        new BindingStructField("bindType", BindingGenerationFixture.NativeInt(), FieldOffset: null),
                        new BindingStructField("@value", BindingGenerationFixture.NativePrimitive("SDL_GameControllerButtonBind_value", "SDL_GameControllerButtonBind_value"), FieldOffset: null),
                    ],
                    Layout: System.Runtime.InteropServices.LayoutKind.Sequential,
                    ExplicitSize: null),
                new BindingStruct(
                    Name: "SDL_GameControllerButtonBind_value",
                    Fields:
                    [
                        new BindingStructField("button", BindingGenerationFixture.NativeInt(), FieldOffset: 0),
                        new BindingStructField("axis", BindingGenerationFixture.NativeInt(), FieldOffset: 0),
                    ],
                    Layout: System.Runtime.InteropServices.LayoutKind.Explicit,
                    ExplicitSize: 8),
            ],
            Enums: [],
            Constants: [],
            Handles: [],
            Callbacks: []);
    }

    public static BindingModel ModelWithRichParseViewEvidence()
    {
        var neutral = new BindingParseView(
            Name: "Neutral",
            PlatformConditionKind: nameof(PlatformConditionKind.Neutral),
            SupportedOsPlatform: null,
            Defines: ["SDL_DECLSPEC=", "SDL_DISABLE_IMMINTRIN_H=1"],
            Undefines: ["__has_builtin"],
            Functions:
            [
                new BindingFunction(
                    "SDL_Init",
                    BindingGenerationFixture.NativeInt(),
                    [new BindingParameter(BindingGenerationFixture.NativeUInt(), "flags")],
                    "SDL.h"),
                new BindingFunction(
                    "SDL_Quit",
                    BindingGenerationFixture.NativeVoid(),
                    [],
                    "SDL.h"),
            ]);

        var linux = new BindingParseView(
            Name: "Linux",
            PlatformConditionKind: nameof(PlatformConditionKind.OperatingSystem),
            SupportedOsPlatform: "linux",
            Defines: ["SDL_VIDEO_DRIVER_X11=1"],
            Undefines: ["__WIN32__"],
            Functions:
            [
                new BindingFunction(
                    "SDL_LinuxSetThreadPriority",
                    BindingGenerationFixture.NativeInt(),
                    [
                        new BindingParameter(BindingGenerationFixture.NativePrimitive("long", "long"), "threadID"),
                        new BindingParameter(BindingGenerationFixture.NativeInt(), "priority"),
                    ],
                    "SDL_system.h"),
            ]);

        return new BindingModel(
            Views: [neutral, linux],
            Structs:
            [
                new BindingStruct(
                    Name: "SDL_Rect",
                    Fields:
                    [
                        new BindingStructField("x", BindingGenerationFixture.NativeInt(), FieldOffset: null),
                    ],
                    Layout: LayoutKind.Sequential,
                    ExplicitSize: null),
            ],
            Enums:
            [
                new BindingEnumeration(
                    Name: "SDL_EventType",
                    UnderlyingType: BindingGenerationFixture.NativeUInt(),
                    IsFlags: false,
                    Members:
                    [
                        new BindingEnumMember("SDL_QUIT", "0x100"),
                    ]),
            ],
            Constants:
            [
                new BindingConstant("SDL_INIT_TIMER", BindingGenerationFixture.NativeUInt(), "0x00000001u", ConstantKind.Literal),
            ],
            Handles:
            [
                new BindingHandle("SDL_Window", NativeTypeRef.OpaqueHandle("SDL_Window", "SDL_Window", "sdl2-core", "SDL_video.h")),
            ],
            Callbacks:
            [
                new BindingCallback("SDL_AudioCallback", BindingGenerationFixture.NativeVoid(), []),
            ])
        {
            MacroReport = new MacroConstantReport(
                ParsedCount: 1,
                CandidateCount: 1,
                EmittedCount: 1,
                SkippedCount: 0,
                ExcludedCount: 0,
                OverriddenCount: 0,
                DuplicateCoalescedCount: 0,
                HelperCandidateCount: 0,
                HelperDuplicateCoalescedCount: 0,
                UnsupportedCount: 0,
                ConflictCount: 0,
                Entries:
                [
                    new MacroConstantReportEntry(
                        Name: "SDL_INIT_TIMER",
                        SourceHeader: "SDL.h",
                        ParseViewName: "Neutral",
                        Disposition: "included",
                        Reason: "manual-include",
                        MacroForm: "manual",
                        Taxonomy: "manual-policy",
                        OriginalExpression: null,
                        ComputedValue: null,
                        EmittedType: "uint",
                        EmittedValue: "0x00000001u"),
                ]),
        };
    }
}
