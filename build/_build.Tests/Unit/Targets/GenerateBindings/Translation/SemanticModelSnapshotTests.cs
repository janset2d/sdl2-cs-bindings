using System.Collections.Immutable;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Translation;
using Build.Tests.Fixtures;
using Build.Tests.Fixtures.GenerateBindings;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

[LinuxOnly]
public sealed class SemanticModelSnapshotTests
{
    [Test]
    public Task Translate_Should_Match_Platform_Conditioned_Function_Snapshot()
    {
        var neutral = SemanticFixtureParser.ParseFixture(
            "GenerateBindings/SemanticTypes/platform-conditioned-functions.h",
            parseAsSdl2Header: true);
        var linux = SemanticFixtureParser.ParseFixture(
            "GenerateBindings/SemanticTypes/platform-conditioned-functions.h",
            parseAsSdl2Header: true,
            defines: ["JANSET_TEST_LINUX_VIEW=1"]);

        var model = CppAstToBindingModel.Translate(
            [
                SemanticFixtureParser.ParseResult("Neutral", neutral),
                SemanticFixtureParser.ParseResult("Linux", linux, defines: ["JANSET_TEST_LINUX_VIEW=1"]),
            ],
            BindingGenerationFixture.Sdl2CoreConfig(),
            requiredFunctions: []);

        return Verify(Project(model));
    }

    [Test]
    public Task Translate_Should_Match_Manual_Macro_Policy_Snapshot()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig(
            requiredConstants:
            [
                BindingGenerationFixture.RequiredConstant("SDL_TEST_REQUIRED", type: "uint", value: "0x40u", sourceHeader: "SDL.h"),
            ]) with
            {
                MacroConstants = new MacroConstantPolicyConfig
                {
                    Excluded = ImmutableDictionary<string, ManualMacroConstantPolicyEntry>.Empty.Add(
                        "SDL_HINT_TEST_BETA",
                        new ManualMacroConstantPolicyEntry { Reason = "test manual exclusion" }),
                    Overrides = ImmutableDictionary<string, MacroConstantOverrideConfig>.Empty.Add(
                        "SDL_FIXTURE_OVERRIDE",
                        new MacroConstantOverrideConfig
                        {
                            Type = "uint",
                            Value = "42u",
                            SourceHeader = "manual-policy-surface.h",
                            Kind = ConstantKind.Literal,
                            Reason = "test manual override",
                        }),
                },
            };
        var compilation = SemanticFixtureParser.ParseFixture(
            "GenerateBindings/MacroConstants/manual-policy-surface.h",
            parseMacros: true,
            parseAsSdl2Header: true);
        var result = BindingConstantTranslator.Translate(
            [SemanticFixtureParser.ParseResult("Neutral", compilation)],
            config);

        return Verify(new
        {
            Constants = result.Constants
                .Select(c => new { c.Name, Type = c.Type.ManagedName, c.Value, c.Kind })
                .OrderBy(c => c.Name, StringComparer.Ordinal)
                .ToArray(),
            Report = result.Report.Entries
                .Where(e => e.Name.StartsWith("SDL_", StringComparison.Ordinal))
                .Select(e => new { e.Name, e.Disposition, e.Reason, e.MacroForm, e.Taxonomy })
                .OrderBy(e => e.Name, StringComparer.Ordinal)
                .ThenBy(e => e.Disposition, StringComparer.Ordinal)
                .ToArray(),
        });
    }

    private static object Project(BindingModel model) => new
    {
        Views = model.Views.Select(view => new
        {
            view.Name,
            Functions = view.Functions.Select(function => new
            {
                function.Name,
                ReturnType = function.ReturnType.ManagedName,
                Parameters = function.Parameters.Select(parameter => new { parameter.Name, Type = parameter.Type.ManagedName }).ToArray(),
            }).ToArray(),
        }).ToArray(),
        Structs = model.Structs.Select(s => s.Name).ToArray(),
        Enums = model.Enums.Select(e => e.Name).ToArray(),
        Handles = model.Handles.Select(h => h.Name).ToArray(),
        Callbacks = model.Callbacks.Select(c => c.Name).ToArray(),
    };
}
