using System.Text.Json;
using Build.Targets.GenerateBindings.Emit.Reports;
using Build.Tests.Unit.Targets.GenerateBindings.Emit;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emit.Reports;

public sealed class BindingParseViewReportBuilderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [Test]
    public async Task Build_Should_Write_Rich_Parse_View_Audit_Report()
    {
        var model = BindingModelData.ModelWithRichParseViewEvidence();
        var emittedFiles = new[]
        {
            "Platform/Neutral/Commands.g.cs",
            "Platform/Linux/Commands.g.cs",
            "Constants.g.cs",
            "Types/Enums.g.cs",
            "Types/Handles.g.cs",
            "Types/Structs.g.cs",
            "Types/Callbacks.g.cs",
            "parse-views.json",
        };

        var report = BindingParseViewReportBuilder.Build(model, emittedFiles);
        var json = JsonSerializer.Serialize(report, JsonOptions);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        await Assert.That(root.GetProperty("SchemaVersion").GetInt32()).IsEqualTo(1);

        var categories = root.GetProperty("Categories");
        await Assert.That(categories.GetProperty("ViewCount").GetInt32()).IsEqualTo(2);
        await Assert.That(categories.GetProperty("FunctionCount").GetInt32()).IsEqualTo(3);
        await Assert.That(categories.GetProperty("StructCount").GetInt32()).IsEqualTo(1);
        await Assert.That(categories.GetProperty("EnumCount").GetInt32()).IsEqualTo(1);
        await Assert.That(categories.GetProperty("ConstantCount").GetInt32()).IsEqualTo(1);
        await Assert.That(categories.GetProperty("HandleCount").GetInt32()).IsEqualTo(1);
        await Assert.That(categories.GetProperty("CallbackCount").GetInt32()).IsEqualTo(1);
        await Assert.That(JsonStrings(categories.GetProperty("Structs"))).IsEquivalentTo(["SDL_Rect"]);
        await Assert.That(JsonStrings(categories.GetProperty("Enums"))).IsEquivalentTo(["SDL_EventType"]);
        await Assert.That(JsonStrings(categories.GetProperty("Constants"))).IsEquivalentTo(["SDL_INIT_TIMER"]);
        await Assert.That(JsonStrings(categories.GetProperty("Handles"))).IsEquivalentTo(["SDL_Window"]);
        await Assert.That(JsonStrings(categories.GetProperty("Callbacks"))).IsEquivalentTo(["SDL_AudioCallback"]);

        await Assert.That(JsonStrings(root.GetProperty("EmittedFiles"))).IsEquivalentTo(emittedFiles);

        var linux = root.GetProperty("Views").EnumerateArray()
            .Single(view => view.GetProperty("Name").GetString() == "Linux");
        await Assert.That(linux.GetProperty("PlatformConditionKind").GetString()).IsEqualTo("OperatingSystem");
        await Assert.That(linux.GetProperty("SupportedOSPlatform").GetString()).IsEqualTo("linux");
        await Assert.That(JsonStrings(linux.GetProperty("Defines"))).IsEquivalentTo(["SDL_VIDEO_DRIVER_X11=1"]);
        await Assert.That(JsonStrings(linux.GetProperty("Undefines"))).IsEquivalentTo(["__WIN32__"]);
        await Assert.That(linux.GetProperty("FunctionCount").GetInt32()).IsEqualTo(1);

        var function = linux.GetProperty("Functions").EnumerateArray().Single();
        await Assert.That(function.GetProperty("Name").GetString()).IsEqualTo("SDL_LinuxSetThreadPriority");
        await Assert.That(function.GetProperty("SourceHeader").GetString()).IsEqualTo("SDL_system.h");
        await Assert.That(function.GetProperty("ReturnType").GetString()).IsEqualTo("int");
        var parameters = function.GetProperty("Parameters").EnumerateArray().ToArray();
        await Assert.That(parameters[0].GetProperty("Name").GetString()).IsEqualTo("threadID");
        await Assert.That(parameters[0].GetProperty("Type").GetString()).IsEqualTo("long");
        await Assert.That(parameters[1].GetProperty("Name").GetString()).IsEqualTo("priority");
        await Assert.That(parameters[1].GetProperty("Type").GetString()).IsEqualTo("int");

        var macroConstants = root.GetProperty("MacroConstants");
        await Assert.That(macroConstants.GetProperty("ParsedCount").GetInt32()).IsEqualTo(1);
        await Assert.That(macroConstants.GetProperty("EmittedCount").GetInt32()).IsEqualTo(1);
        await Assert.That(macroConstants.GetProperty("HelperCandidateCount").GetInt32()).IsEqualTo(0);
        await Assert.That(macroConstants.GetProperty("HelperDuplicateCoalescedCount").GetInt32()).IsEqualTo(0);
        var macroEntries = macroConstants.GetProperty("Entries").EnumerateArray().ToArray();
        await Assert.That(macroEntries.Length).IsEqualTo(1);
        var timerEntry = macroEntries[0];
        await Assert.That(timerEntry.GetProperty("Name").GetString()).IsEqualTo("SDL_INIT_TIMER");
        await Assert.That(timerEntry.GetProperty("Disposition").GetString()).IsEqualTo("included");
        await Assert.That(timerEntry.GetProperty("Reason").GetString()).IsEqualTo("manual-include");
        await Assert.That(timerEntry.GetProperty("EmittedType").GetString()).IsEqualTo("uint");
        await Assert.That(timerEntry.GetProperty("EmittedValue").GetString()).IsEqualTo("0x00000001u");
        await Assert.That(timerEntry.GetProperty("MacroForm").GetString()).IsEqualTo("manual");
        await Assert.That(timerEntry.GetProperty("Taxonomy").GetString()).IsEqualTo("manual-policy");
        await Assert.That(timerEntry.GetProperty("OriginalExpression").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(timerEntry.GetProperty("ComputedValue").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    private static string[] JsonStrings(JsonElement array)
    {
        return [.. array.EnumerateArray().Select(element => element.GetString() ?? string.Empty)];
    }
}
