using System.Text.Json;
using Build.Targets.GenerateBindings.Emit.Reports;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emit.Reports;

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
            ],
            MacroConstants: new BindingMacroConstantsReport(
                ParsedCount: 1,
                CandidateCount: 1,
                EmittedCount: 1,
                SkippedCount: 0,
                ExcludedCount: 0,
                OverriddenCount: 0,
                DuplicateCoalescedCount: 0,
                HelperCandidateCount: 1,
                HelperDuplicateCoalescedCount: 0,
                UnsupportedCount: 0,
                ConflictCount: 0,
                Entries:
                [
                    new BindingMacroConstantReportEntry(
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
                ]));

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
        await Assert.That(round.MacroConstants.ParsedCount).IsEqualTo(1);
        await Assert.That(round.MacroConstants.EmittedCount).IsEqualTo(1);
        await Assert.That(round.MacroConstants.HelperCandidateCount).IsEqualTo(1);
        await Assert.That(round.MacroConstants.HelperDuplicateCoalescedCount).IsEqualTo(0);
        await Assert.That(round.MacroConstants.Entries.Count).IsEqualTo(1);
        var macroEntry = round.MacroConstants.Entries[0];
        await Assert.That(macroEntry.Name).IsEqualTo("SDL_INIT_TIMER");
        await Assert.That(macroEntry.Disposition).IsEqualTo("included");
        await Assert.That(macroEntry.Reason).IsEqualTo("manual-include");
        await Assert.That(macroEntry.MacroForm).IsEqualTo("manual");
        await Assert.That(macroEntry.Taxonomy).IsEqualTo("manual-policy");
        await Assert.That(macroEntry.OriginalExpression).IsNull();
        await Assert.That(macroEntry.ComputedValue).IsNull();
        await Assert.That(macroEntry.EmittedType).IsEqualTo("uint");
        await Assert.That(macroEntry.EmittedValue).IsEqualTo("0x00000001u");
    }
}
