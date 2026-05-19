using System.Text.Json;
using Build.Targets.GenerateBindings.Emitting;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emitting;

public sealed class BindingParseViewReportTests
{
    [Test]
    public async Task BindingParseViewReport_Should_Round_Trip_Through_System_Text_Json()
    {
        var original = new BindingParseViewReport(
            SchemaVersion: 1,
            Categories: new BindingParseViewReportCategories(
                ViewCount: 2,
                FunctionCount: 3,
                StructCount: 1,
                EnumCount: 1,
                ConstantCount: 1,
                HandleCount: 1,
                CallbackCount: 1,
                Structs: ["SDL_Rect"],
                Enums: ["SDL_EventType"],
                Constants: ["SDL_INIT_TIMER"],
                Handles: ["SDL_Window"],
                Callbacks: ["SDL_AudioCallback"]),
            EmittedFiles:
            [
                "Platform/Neutral/Commands.g.cs",
                "Platform/Linux/Commands.g.cs",
                "parse-views.json",
            ],
            Views:
            [
                new BindingParseViewReportEntry(
                    Name: "Neutral",
                    PlatformConditionKind: "Neutral",
                    SupportedOSPlatform: null,
                    Defines: ["SDL_DECLSPEC="],
                    Undefines: ["__has_builtin"],
                    FunctionCount: 2,
                    Functions:
                    [
                        new BindingParseViewReportFunction(
                            "SDL_Init",
                            "SDL.h",
                            "int",
                            [new BindingParseViewReportParameter("flags", "uint")]),
                        new BindingParseViewReportFunction("SDL_Quit", "SDL.h", "void", []),
                    ]),
                new BindingParseViewReportEntry(
                    Name: "Linux",
                    PlatformConditionKind: "OperatingSystem",
                    SupportedOSPlatform: "linux",
                    Defines: ["SDL_VIDEO_DRIVER_X11=1"],
                    Undefines: ["__WIN32__"],
                    FunctionCount: 1,
                    Functions:
                    [
                        new BindingParseViewReportFunction("SDL_LinuxSetThreadPriority", "SDL_system.h", "int", []),
                    ]),
            ]);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(original, options);
        var round = JsonSerializer.Deserialize<BindingParseViewReport>(json, options);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Views.Count).IsEqualTo(2);
        await Assert.That(round.SchemaVersion).IsEqualTo(1);
        await Assert.That(round.Categories.Structs).IsEquivalentTo(["SDL_Rect"]);
        await Assert.That(round.EmittedFiles).Contains("parse-views.json");
        await Assert.That(round.Views[0].Name).IsEqualTo("Neutral");
        await Assert.That(round.Views[0].PlatformConditionKind).IsEqualTo("Neutral");
        await Assert.That(round.Views[0].Defines).IsEquivalentTo(["SDL_DECLSPEC="]);
        await Assert.That(round.Views[0].Functions.Count).IsEqualTo(2);
        await Assert.That(round.Views[1].SupportedOSPlatform).IsEqualTo("linux");
        await Assert.That(round.Views[0].Functions[0].ReturnType).IsEqualTo("int");
        await Assert.That(round.Views[0].Functions[0].Parameters.Single().Type).IsEqualTo("uint");
    }
}
