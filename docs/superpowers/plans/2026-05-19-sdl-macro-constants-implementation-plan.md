# SDL Macro Constants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Auto-generate SDL2.Core object-like `SDL_*` macro constants from parsed CppAst header data with deterministic duplicate detection, manual policy escape hatches, and parse-report evidence.

**Architecture:** Add a source-first macro translation lane inside the existing `GenerateBindings` semantic pipeline. CppAst macro data flows through focused collectors, policy/classification, merge/manual-policy validation, then into `BindingModel.Constants` and `parse-views.json`; `required_constants` remains only a manual include path.

**Tech Stack:** .NET 10, C# 14, Cake Frosting, CppAst 0.24.0, TUnit on Microsoft.Testing.Platform, System.Text.Json, existing `tools.cs generate-bindings` and binding compile-check gates.

---

## Execution Guardrails

- Use TDD: write the failing test first for every task.
- Do not commit without Deniz's explicit approval. Each commit step below is an approval checkpoint with a proposed conventional commit message, not permission to run `git commit`.
- Use `apply_patch` for manual edits.
- Keep new production files under `build\_build\Targets\GenerateBindings\Translation\` unless the file is clearly model/report/config surface.
- Keep test fixture headers under `build\_build.Tests\Fixtures\Data\GenerateBindings\`.
- Run slopwatch after code/test/project changes:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

## File Structure

### Production files to create

- `build\_build\Targets\GenerateBindings\Translation\MacroConstantCandidate.cs`
  - Raw macro fact from CppAst plus parse-view/source evidence.
- `build\_build\Targets\GenerateBindings\Translation\MacroCandidateCollector.cs`
  - Extracts `CppCompilation.Macros` from `CppAstParseResult`.
- `build\_build\Targets\GenerateBindings\Translation\MacroApiPolicy.cs`
  - Classifies raw macros as candidate, non-API, or unsupported before value translation.
- `build\_build\Targets\GenerateBindings\Translation\MacroValueClassifier.cs`
  - Converts safe object-like macro values into `BindingConstant` shapes.
- `build\_build\Targets\GenerateBindings\Translation\MacroConstantMerger.cs`
  - Coalesces compatible cross-view definitions and fails incompatible duplicates.
- `build\_build\Targets\GenerateBindings\Translation\MacroManualPolicyApplier.cs`
  - Applies manifest include/exclude/override policy and validates stale entries.
- `build\_build\Targets\GenerateBindings\Translation\BindingConstantTranslator.cs`
  - Orchestrates collector, policy, classifier, merger, manual policy, and report output.
- `build\_build\Targets\GenerateBindings\Model\MacroConstantReport.cs`
  - Semantic macro audit report carried on `BindingModel`.

### Production files to modify

- `build\_build\Data\BindingGeneration\Models\BindingGenerationConfig.cs`
  - Add macro manual policy config records and optional stale escape hatch fields.
- `build\_build\Targets\GenerateBindings\Model\BindingConstant.cs`
  - Update comments to source-first macro reality.
- `build\_build\Targets\GenerateBindings\Model\BindingModel.cs`
  - Add `MacroReport` init property with an empty default.
- `build\_build\Targets\GenerateBindings\Translation\CppAstToBindingModel.cs`
  - Replace direct `RequiredConstantTranslator.Translate(...)` usage with `BindingConstantTranslator`.
- `build\_build\Targets\GenerateBindings\Translation\RequiredConstantTranslator.cs`
  - Keep as include/seed adapter and update comments to describe the manual include role.
- `build\_build\Targets\GenerateBindings\Emitting\BindingParseViewReport.cs`
  - Add macro-report JSON records.
- `build\_build\Targets\GenerateBindings\Emitting\CsCommandEmitter.cs`
  - Include macro report data in `parse-views.json`.
- `build\manifest.json`
  - Remove `SDL_HINT_RENDER_DRIVER` from `required_constants` once source generation proves it.

### Test files to create

- `build\_build.Tests\Fixtures\Data\GenerateBindings\MacroConstants\macro-constants.h`
  - Embedded fixture header for real CppAst macro shape tests.
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroCandidateCollectorTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroApiPolicyTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroValueClassifierTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroConstantMergerTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroManualPolicyApplierTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingConstantTranslatorTests.cs`

### Test files to modify

- `build\_build.Tests\Fixtures\BindingGenerationFixture.cs`
- `build\_build.Tests\Fixtures\Data\Manifest\manifest-real.json`
- `build\_build.Tests\Unit\Data\BindingGeneration\BindingGenerationConfigRepositoryTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Parsing\SemanticHeaderFixtureTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\CppAstToBindingModelTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\BindingModelData.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\BindingParseViewReportTests.cs`
- `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\CsCommandEmitterTests.cs`
- `build\_build.Tests\Scenarios\GenerateBindings\GenerateBindingsTaskScenarioTests.cs`

### Documentation files to modify

- `docs\binding-autogen\binding-api-surface-strategy.md`
- `docs\playbook\binding-generator-maintenance.md`
- `docs\plan.md`

---

## Task 1: Manifest Macro Manual Policy Config

**Files:**
- Modify: `build\_build\Data\BindingGeneration\Models\BindingGenerationConfig.cs:16-140`
- Modify: `build\_build.Tests\Fixtures\BindingGenerationFixture.cs:22-84`
- Modify: `build\_build.Tests\Fixtures\Data\Manifest\manifest-real.json:213-218`
- Modify: `build\_build.Tests\Unit\Data\BindingGeneration\BindingGenerationConfigRepositoryTests.cs:51-100`

- [ ] **Step 1: Write failing config round-trip assertions**

Add assertions to `Load_Should_Return_Sdl2Core_Config_When_Manifest_Has_Enabled_Block`:

```csharp
await Assert.That(config.MacroConstants.Excluded.ContainsKey("SDL_PRIVATE_HEADER_SWITCH")).IsTrue();
await Assert.That(config.MacroConstants.Excluded["SDL_PRIVATE_HEADER_SWITCH"].Reason)
    .IsEqualTo("Fixture-only non-API macro exclusion.");
await Assert.That(config.MacroConstants.Excluded["SDL_PRIVATE_HEADER_SWITCH"].AllowStale).IsTrue();

await Assert.That(config.MacroConstants.Overrides.ContainsKey("SDL_FIXTURE_OVERRIDE")).IsTrue();
var macroOverride = config.MacroConstants.Overrides["SDL_FIXTURE_OVERRIDE"];
await Assert.That(macroOverride.Type).IsEqualTo("uint");
await Assert.That(macroOverride.Value).IsEqualTo("42u");
await Assert.That(macroOverride.SourceHeader).IsEqualTo("SDL_fixture.h");
await Assert.That(macroOverride.Kind).IsEqualTo(ConstantKind.Literal);
await Assert.That(macroOverride.Reason).IsEqualTo("Fixture-only override.");
await Assert.That(macroOverride.AllowStale).IsFalse();
```

Also assert the optional include escape hatch on the existing required constants:

```csharp
await Assert.That(config.RequiredConstants[0].AllowStale).IsFalse();
await Assert.That(config.RequiredConstants[0].Reason).IsNull();
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingGenerationConfigRepositoryRoundTripTests"
```

Expected: FAIL because `BindingGenerationConfig` has no `MacroConstants` property and `RequiredConstantConfig` has no `AllowStale` / `Reason` properties.

- [ ] **Step 3: Add config model records**

In `BindingGenerationConfig.cs`, add the property near `RequiredConstants`:

```csharp
[JsonPropertyName("macro_constants")]
public MacroConstantPolicyConfig MacroConstants { get; init; } = new();
```

Add optional include metadata to `RequiredConstantConfig`:

```csharp
[JsonPropertyName("reason")] public string? Reason { get; init; }
[JsonPropertyName("allow_stale")] public bool AllowStale { get; init; }
```

Add these records below `RequiredConstantConfig`:

```csharp
public sealed record MacroConstantPolicyConfig
{
    [JsonPropertyName("excluded")]
    public ImmutableDictionary<string, ManualMacroConstantPolicyEntry> Excluded { get; init; } =
        ImmutableDictionary<string, ManualMacroConstantPolicyEntry>.Empty;

    [JsonPropertyName("overrides")]
    public ImmutableDictionary<string, MacroConstantOverrideConfig> Overrides { get; init; } =
        ImmutableDictionary<string, MacroConstantOverrideConfig>.Empty;
}

public sealed record ManualMacroConstantPolicyEntry
{
    [JsonPropertyName("reason")] public required string Reason { get; init; }
    [JsonPropertyName("allow_stale")] public bool AllowStale { get; init; }
}

public sealed record MacroConstantOverrideConfig
{
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("value")] public required string Value { get; init; }
    [JsonPropertyName("source_header")] public required string SourceHeader { get; init; }
    [JsonPropertyName("kind")] public required ConstantKind Kind { get; init; }
    [JsonPropertyName("reason")] public required string Reason { get; init; }
    [JsonPropertyName("allow_stale")] public bool AllowStale { get; init; }
}
```

- [ ] **Step 4: Seed fixture-only macro policy JSON**

In `build\_build.Tests\Fixtures\Data\Manifest\manifest-real.json`, add this sibling after `required_constants`:

```json
"macro_constants": {
  "excluded": {
    "SDL_PRIVATE_HEADER_SWITCH": {
      "reason": "Fixture-only non-API macro exclusion.",
      "allow_stale": true
    }
  },
  "overrides": {
    "SDL_FIXTURE_OVERRIDE": {
      "type": "uint",
      "value": "42u",
      "source_header": "SDL_fixture.h",
      "kind": "Literal",
      "reason": "Fixture-only override."
    }
  }
},
```

Keep `build\manifest.json` unchanged in this task. The live manifest does not need manual macro policy yet.

- [ ] **Step 5: Update fixture builder defaults**

In `BindingGenerationFixture.Sdl2CoreConfig`, initialize the new property so tests can override it later:

```csharp
MacroConstants = new MacroConstantPolicyConfig(),
```

- [ ] **Step 6: Run focused tests and verify pass**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingGenerationConfigRepositoryRoundTripTests"
```

Expected: PASS for the repository round-trip suite.

- [ ] **Step 7: Commit approval checkpoint**

Show Deniz this proposed message and wait:

```text
test: add macro constant policy config coverage
```

---

## Task 2: Raw Macro Candidate Collection

**Files:**
- Create: `build\_build\Targets\GenerateBindings\Translation\MacroConstantCandidate.cs`
- Create: `build\_build\Targets\GenerateBindings\Translation\MacroCandidateCollector.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroCandidateCollectorTests.cs`
- Create: `build\_build.Tests\Fixtures\Data\GenerateBindings\MacroConstants\macro-constants.h`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\SemanticHeaderFixtureTests.cs:87-115`

- [ ] **Step 1: Add macro fixture header**

Create `build\_build.Tests\Fixtures\Data\GenerateBindings\MacroConstants\macro-constants.h`:

```c
#ifndef SDL_FIXTURE_MACRO_CONSTANTS_H_
#define SDL_FIXTURE_MACRO_CONSTANTS_H_

#define SDL_HINT_RENDER_DRIVER "SDL_RENDER_DRIVER"
#define SDL_HINT_RENDER_VSYNC "SDL_RENDER_VSYNC"
#define SDL_INIT_TIMER 0x00000001u
#define SDL_BUTTON_LEFT 1
#define SDL_BUTTON(X) (1u << ((X) - 1))
#define SDL_PRIVATE_HEADER_SWITCH
#define SDL_PLATFORM_WINDOWS 1
#define NOT_SDL_VALUE 7

#endif
```

- [ ] **Step 2: Write failing real CppAst macro shape test**

In `SemanticHeaderFixtureTests.cs`, add:

```csharp
[Test]
public async Task Fixtures_Should_Parse_Object_Like_And_Function_Like_Macros()
{
    var compilation = ParseFixture("GenerateBindings/MacroConstants/macro-constants.h", parseMacros: true);

    var renderDriver = compilation.Macros.Single(m => m.Name == "SDL_HINT_RENDER_DRIVER");
    await Assert.That(renderDriver.Value).IsEqualTo("\"SDL_RENDER_DRIVER\"");
    await Assert.That(renderDriver.Parameters).IsEmpty();

    var button = compilation.Macros.Single(m => m.Name == "SDL_BUTTON");
    await Assert.That(button.Parameters).IsEquivalentTo(["X"]);
    await Assert.That(button.Value).Contains("1u");
}
```

Do not change the helper signature yet.

- [ ] **Step 3: Run the focused fixture test and verify it fails before helper update**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~SemanticHeaderFixtureTests"
```

Expected: FAIL until the helper accepts `parseMacros` and the fixture resource path exists.

- [ ] **Step 4: Add candidate record**

Create `MacroConstantCandidate.cs`:

```csharp
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed record MacroConstantCandidate(
    string Name,
    string Value,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<CppToken> Tokens,
    string SourceFile,
    string SourceHeader,
    string ParseViewName)
{
    public bool IsFunctionLike => Parameters.Count > 0;
}
```

- [ ] **Step 5: Add collector tests using constructed CppMacro values**

Create `MacroCandidateCollectorTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Translation;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class MacroCandidateCollectorTests
{
    [Test]
    public async Task Collect_Should_Return_Macros_With_View_And_Source_Evidence()
    {
        var compilation = new CppCompilation();
        compilation.Macros.Add(Macro("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\"", "SDL_hints.h"));

        var result = new CppAstParseResult(ParseView("Neutral"), [compilation]);

        var candidates = new MacroCandidateCollector().Collect([result]);

        await Assert.That(candidates.Count).IsEqualTo(1);
        await Assert.That(candidates[0].Name).IsEqualTo("SDL_HINT_RENDER_DRIVER");
        await Assert.That(candidates[0].Value).IsEqualTo("\"SDL_RENDER_DRIVER\"");
        await Assert.That(candidates[0].SourceHeader).IsEqualTo("SDL_hints.h");
        await Assert.That(candidates[0].ParseViewName).IsEqualTo("Neutral");
    }

    [Test]
    public async Task Collect_Should_Preserve_Function_Like_Parameters()
    {
        var compilation = new CppCompilation();
        var macro = Macro("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h");
        macro.Parameters = ["X"];
        compilation.Macros.Add(macro);

        var candidates = new MacroCandidateCollector().Collect([new CppAstParseResult(ParseView("Neutral"), [compilation])]);

        await Assert.That(candidates.Single().IsFunctionLike).IsTrue();
        await Assert.That(candidates.Single().Parameters).IsEquivalentTo(["X"]);
    }

    private static PlatformParseView ParseView(string name) =>
        new(name, PlatformConditionKind.Neutral, SupportedOsPlatform: null, Defines: [], Undefines: []);

    private static CppMacro Macro(string name, string value, string header)
    {
        var macro = new CppMacro(name) { Value = value };
        macro.Span = new CppSourceSpan(
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", 0, 1, 1),
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", 1, 1, 2));
        macro.Tokens.Add(new CppToken(CppTokenKind.Literal, value));
        return macro;
    }
}
```

- [ ] **Step 6: Run collector tests and verify they fail**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~MacroCandidateCollectorTests"
```

Expected: FAIL because `MacroCandidateCollector` does not exist.

- [ ] **Step 7: Implement collector**

Create `MacroCandidateCollector.cs`:

```csharp
using Build.Targets.GenerateBindings.Parsing;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed class MacroCandidateCollector
{
    public IReadOnlyList<MacroConstantCandidate> Collect(IReadOnlyList<CppAstParseResult> parseResults)
    {
        ArgumentNullException.ThrowIfNull(parseResults);

        var candidates = new List<MacroConstantCandidate>();
        foreach (var result in parseResults)
        {
            foreach (var compilation in result.Compilations)
            {
                foreach (var macro in compilation.Macros)
                {
                    var sourceFile = macro.SourceFile ?? string.Empty;
                    candidates.Add(new MacroConstantCandidate(
                        macro.Name,
                        macro.Value,
                        macro.Parameters,
                        macro.Tokens,
                        sourceFile,
                        Path.GetFileName(sourceFile),
                        result.ParseView.Name));
                }
            }
        }

        return candidates
            .OrderBy(candidate => candidate.ParseViewName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SourceFile, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ToList();
    }
}
```

- [ ] **Step 8: Enable macro parsing in the fixture helper**

In `SemanticHeaderFixtureTests.cs`, change the helper signature:

```csharp
private static CppCompilation ParseFixture(string fixturePath, bool parseMacros = false)
```

Change the parser options assignment:

```csharp
ParseMacros = parseMacros,
```

- [ ] **Step 9: Run focused tests and verify pass**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~MacroCandidateCollectorTests|FullyQualifiedName~SemanticHeaderFixtureTests"
```

Expected: PASS for the collector tests and existing semantic fixture tests. On non-Linux hosts, `[LinuxOnly]` fixture tests may skip; the constructed collector tests must pass.

- [ ] **Step 10: Commit approval checkpoint**

Show Deniz this proposed message and wait:

```text
feat: collect raw SDL macro candidates from CppAst
```

---

## Task 3: Macro API Policy and Value Classification

**Files:**
- Create: `build\_build\Targets\GenerateBindings\Translation\MacroApiPolicy.cs`
- Create: `build\_build\Targets\GenerateBindings\Translation\MacroValueClassifier.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\MacroConstantReport.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroApiPolicyTests.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroValueClassifierTests.cs`

- [ ] **Step 1: Write failing API policy tests**

Create `MacroApiPolicyTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Translation;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class MacroApiPolicyTests
{
    [Test]
    public async Task Classify_Should_Accept_Object_Like_Sdl_Macro_From_Sdl2_Header()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\"", "SDL_hints.h"));

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.Candidate);
        await Assert.That(decision.Reason).IsEqualTo("object-like SDL macro");
    }

    [Test]
    public async Task Classify_Should_Report_Function_Like_Macro_As_Unsupported()
    {
        var candidate = Candidate("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h", parameters: ["X"]);

        var decision = MacroApiPolicy.Classify(candidate);

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.Unsupported);
        await Assert.That(decision.Reason).IsEqualTo("function-like macro");
    }

    [Test]
    public async Task Classify_Should_Report_Include_Guard_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_hints_h_", string.Empty, "SDL_hints.h"));

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        await Assert.That(decision.Reason).IsEqualTo("include guard or empty macro");
    }

    [Test]
    public async Task Classify_Should_Report_Platform_Control_Define_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("SDL_PLATFORM_WINDOWS", "1", "SDL_platform.h"));

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        await Assert.That(decision.Reason).IsEqualTo("platform control macro");
    }

    [Test]
    public async Task Classify_Should_Report_Non_Sdl_Name_As_Non_Api()
    {
        var decision = MacroApiPolicy.Classify(Candidate("NOT_SDL_VALUE", "7", "SDL_hints.h"));

        await Assert.That(decision.Disposition).IsEqualTo(MacroApiDisposition.NonApi);
        await Assert.That(decision.Reason).IsEqualTo("non-SDL macro");
    }

    private static MacroConstantCandidate Candidate(string name, string value, string header, IReadOnlyList<string>? parameters = null) =>
        new(name, value, parameters ?? [], [], $"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", header, "Neutral");
}
```

- [ ] **Step 2: Run API policy tests and verify they fail**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~MacroApiPolicyTests"
```

Expected: FAIL because `MacroApiPolicy` and related types do not exist.

- [ ] **Step 3: Implement API policy**

Create `MacroApiPolicy.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Translation;

internal enum MacroApiDisposition
{
    Candidate,
    NonApi,
    Unsupported,
}

internal sealed record MacroApiDecision(MacroApiDisposition Disposition, string Reason);

internal static class MacroApiPolicy
{
    public static MacroApiDecision Classify(MacroConstantCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!candidate.Name.StartsWith("SDL_", StringComparison.Ordinal))
            return new MacroApiDecision(MacroApiDisposition.NonApi, "non-SDL macro");

        if (!BindableDeclarationPolicy.IsSdl2Header(candidate.SourceFile))
            return new MacroApiDecision(MacroApiDisposition.NonApi, "non-SDL2 header");

        if (candidate.IsFunctionLike)
            return new MacroApiDecision(MacroApiDisposition.Unsupported, "function-like macro");

        if (string.IsNullOrWhiteSpace(candidate.Value) || candidate.Name.EndsWith("_H_", StringComparison.OrdinalIgnoreCase))
            return new MacroApiDecision(MacroApiDisposition.NonApi, "include guard or empty macro");

        if (candidate.Name.StartsWith("SDL_PLATFORM_", StringComparison.Ordinal))
            return new MacroApiDecision(MacroApiDisposition.NonApi, "platform control macro");

        if (candidate.Name is "SDL_DECLSPEC" or "SDL_FORCE_INLINE" or "SDL_INLINE" or "SDL_UNUSED")
            return new MacroApiDecision(MacroApiDisposition.NonApi, "compiler control macro");

        return new MacroApiDecision(MacroApiDisposition.Candidate, "object-like SDL macro");
    }
}
```

- [ ] **Step 4: Add macro report model**

Create `MacroConstantReport.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

public sealed record MacroConstantReport(
    int ParsedCount,
    int CandidateCount,
    int EmittedCount,
    int SkippedCount,
    int ExcludedCount,
    int OverriddenCount,
    int DuplicateCoalescedCount,
    int UnsupportedCount,
    int ConflictCount,
    IReadOnlyList<MacroConstantReportEntry> Entries)
{
    public static MacroConstantReport Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, []);
}

public sealed record MacroConstantReportEntry(
    string Name,
    string SourceHeader,
    string ParseViewName,
    string Disposition,
    string Reason,
    string? EmittedType,
    string? EmittedValue);
```

- [ ] **Step 5: Write failing value classifier tests**

Create `MacroValueClassifierTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Translation;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class MacroValueClassifierTests
{
    [Test]
    public async Task Classify_Should_Emit_String_Literal_As_ReadOnlySpan_Utf8()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\""));

        await Assert.That(result.Constant).IsNotNull();
        await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("ReadOnlySpan<byte>");
        await Assert.That(result.Constant.Value).IsEqualTo("\"SDL_RENDER_DRIVER\"u8");
        await Assert.That(result.Report.Disposition).IsEqualTo("emitted");
    }

    [Test]
    public async Task Classify_Should_Emit_Unsigned_Hex_Literal_As_Uint()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_INIT_TIMER", "0x00000001u"));

        await Assert.That(result.Constant).IsNotNull();
        await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("uint");
        await Assert.That(result.Constant.Value).IsEqualTo("0x00000001u");
    }

    [Test]
    public async Task Classify_Should_Emit_Decimal_Literal_As_Int()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_BUTTON_LEFT", "1"));

        await Assert.That(result.Constant).IsNotNull();
        await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("int");
        await Assert.That(result.Constant.Value).IsEqualTo("1");
    }

    [Test]
    public async Task Classify_Should_Emit_Escape_Character_Literal_As_Int()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDLK_ESCAPE_FIXTURE", "'\\033'"));

        await Assert.That(result.Constant).IsNotNull();
        await Assert.That(result.Constant!.Type.ManagedName).IsEqualTo("int");
        await Assert.That(result.Constant.Value).IsEqualTo("27");
    }

    [Test]
    public async Task Classify_Should_Report_Unsupported_Expression()
    {
        var result = MacroValueClassifier.Classify(Candidate("SDL_UNSAFE_EXPR", "(SDL_SOMETHING(type, value))"));

        await Assert.That(result.Constant).IsNull();
        await Assert.That(result.Report.Disposition).IsEqualTo("unsupported");
        await Assert.That(result.Report.Reason).IsEqualTo("unsupported macro expression");
    }

    private static MacroConstantCandidate Candidate(string name, string value) =>
        new(name, value, [], [], "C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/SDL_fixture.h", "SDL_fixture.h", "Neutral");
}
```

- [ ] **Step 6: Run value classifier tests and verify they fail**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~MacroValueClassifierTests"
```

Expected: FAIL because `MacroValueClassifier` does not exist.

- [ ] **Step 7: Implement conservative value classifier**

Create `MacroValueClassifier.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed record MacroValueClassification(BindingConstant? Constant, MacroConstantReportEntry Report);

internal static partial class MacroValueClassifier
{
    public static MacroValueClassification Classify(MacroConstantCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var value = candidate.Value.Trim();
        if (IsStringLiteral(value))
        {
            return Emitted(candidate, new BindingConstant(
                candidate.Name,
                new NativeTypeRef(
                    "const char[]",
                    "ReadOnlySpan<byte>",
                    NativeTypeKind.SubstitutedManagedType,
                    PointerDepth: 0,
                    ElementType: null,
                    SourceHeader: candidate.SourceHeader,
                    AbiShape: NativeAbiShape.Of("ReadOnlySpan<byte>", IntPtr.Size * 2, isBlittable: false),
                    ArrayLength: null,
                    Diagnostics: []),
                value + "u8",
                ConstantKind.Literal));
        }

        if (TryFormatCharacterLiteral(value, out var characterValue))
        {
            return Emitted(candidate, new BindingConstant(
                candidate.Name,
                NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4), candidate.SourceHeader),
                characterValue,
                ConstantKind.Literal));
        }

        if (TryClassifyNumericLiteral(value, out var nativeType, out var managedType))
        {
            var abiSize = managedType == "ulong" ? 8 : 4;
            return Emitted(candidate, new BindingConstant(
                candidate.Name,
                NativeTypeRef.Primitive(nativeType, managedType, NativeAbiShape.Of(managedType, abiSize), candidate.SourceHeader),
                value,
                ConstantKind.Literal));
        }

        return new MacroValueClassification(
            null,
            Report(candidate, "unsupported", "unsupported macro expression", null, null));
    }

    private static MacroValueClassification Emitted(MacroConstantCandidate candidate, BindingConstant constant) =>
        new(constant, Report(candidate, "emitted", "safe macro value", constant.Type.ManagedName, constant.Value));

    private static MacroConstantReportEntry Report(
        MacroConstantCandidate candidate,
        string disposition,
        string reason,
        string? emittedType,
        string? emittedValue) =>
        new(candidate.Name, candidate.SourceHeader, candidate.ParseViewName, disposition, reason, emittedType, emittedValue);

    private static bool IsStringLiteral(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"';

    private static bool TryFormatCharacterLiteral(string value, out string formatted)
    {
        formatted = string.Empty;
        if (value.Length < 3 || value[0] != '\'' || value[^1] != '\'')
            return false;

        var body = value[1..^1];
        var numericValue = body switch
        {
            "\\0" => 0,
            "\\033" => 27,
            "\\x1B" or "\\x1b" => 27,
            { Length: 1 } => body[0],
            _ => -1,
        };

        if (numericValue < 0)
            return false;

        formatted = numericValue.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryClassifyNumericLiteral(string value, out string nativeType, out string managedType)
    {
        nativeType = string.Empty;
        managedType = string.Empty;

        if (!NumericLiteralRegex().IsMatch(value))
            return false;

        if (value.Contains('l', StringComparison.OrdinalIgnoreCase))
            return false;

        var unsigned = value.EndsWith('u') || value.EndsWith('U');
        var digits = unsigned ? value[..^1] : value;
        var isHex = digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        var parseText = isHex ? digits[2..] : digits.TrimStart('-');
        var style = isHex ? NumberStyles.HexNumber : NumberStyles.None;
        if (!ulong.TryParse(parseText, style, CultureInfo.InvariantCulture, out var parsed))
            return false;

        if (!unsigned && digits.StartsWith('-') && parsed <= (ulong)int.MaxValue + 1UL)
        {
            nativeType = "int";
            managedType = "int";
            return true;
        }

        if (!unsigned && parsed <= int.MaxValue)
        {
            nativeType = "int";
            managedType = "int";
            return true;
        }

        if (parsed <= uint.MaxValue)
        {
            nativeType = "unsigned int";
            managedType = "uint";
            return true;
        }

        nativeType = "unsigned long long";
        managedType = "ulong";
        return true;
    }

    [GeneratedRegex(@"^-?(0x[0-9A-Fa-f]+|[0-9]+)([uU])?$")]
    private static partial Regex NumericLiteralRegex();
}
```

- [ ] **Step 8: Run policy and classifier tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~MacroApiPolicyTests|FullyQualifiedName~MacroValueClassifierTests"
```

Expected: PASS.

- [ ] **Step 9: Commit approval checkpoint**

Show Deniz this proposed message and wait:

```text
feat: classify SDL macro constant values
```

---

## Task 4: Duplicate Merge and Manual Policy Application

**Files:**
- Create: `build\_build\Targets\GenerateBindings\Translation\MacroConstantMerger.cs`
- Create: `build\_build\Targets\GenerateBindings\Translation\MacroManualPolicyApplier.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroConstantMergerTests.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\MacroManualPolicyApplierTests.cs`

- [ ] **Step 1: Write failing merger tests**

Create `MacroConstantMergerTests.cs`:

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Translation;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class MacroConstantMergerTests
{
    [Test]
    public async Task Merge_Should_Coalesce_Compatible_Same_Name_Definitions()
    {
        var first = Constant("SDL_HINT_RENDER_DRIVER", "ReadOnlySpan<byte>", "\"SDL_RENDER_DRIVER\"u8");
        var second = Constant("SDL_HINT_RENDER_DRIVER", "ReadOnlySpan<byte>", "\"SDL_RENDER_DRIVER\"u8");

        var result = MacroConstantMerger.Merge([
            new MacroValueClassification(first, Report("SDL_HINT_RENDER_DRIVER", "Neutral")),
            new MacroValueClassification(second, Report("SDL_HINT_RENDER_DRIVER", "Linux")),
        ]);

        await Assert.That(result.Constants.Count).IsEqualTo(1);
        await Assert.That(result.DuplicateCoalescedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Merge_Should_Throw_When_Same_Name_Definitions_Are_Incompatible()
    {
        var first = Constant("SDL_HINT_RENDER_DRIVER", "ReadOnlySpan<byte>", "\"SDL_RENDER_DRIVER\"u8");
        var second = Constant("SDL_HINT_RENDER_DRIVER", "ReadOnlySpan<byte>", "\"BROKEN\"u8");

        await Assert.That(() => MacroConstantMerger.Merge([
            new MacroValueClassification(first, Report("SDL_HINT_RENDER_DRIVER", "Neutral")),
            new MacroValueClassification(second, Report("SDL_HINT_RENDER_DRIVER", "Linux")),
        ])).Throws<InvalidOperationException>()
            .WithMessageContaining("Incompatible macro constant definitions for 'SDL_HINT_RENDER_DRIVER'");
    }

    private static BindingConstant Constant(string name, string type, string value) =>
        new(name, BindingGenerationFixture.NativePrimitive(type, type), value, ConstantKind.Literal);

    private static MacroConstantReportEntry Report(string name, string view) =>
        new(name, "SDL_hints.h", view, "emitted", "safe macro value", "ReadOnlySpan<byte>", "\"SDL_RENDER_DRIVER\"u8");
}
```

- [ ] **Step 2: Run merger tests and verify they fail**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~MacroConstantMergerTests"
```

Expected: FAIL because `MacroConstantMerger` does not exist.

- [ ] **Step 3: Implement merger**

Create `MacroConstantMerger.cs`:

```csharp
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed record MacroConstantMergeResult(
    IReadOnlyList<BindingConstant> Constants,
    IReadOnlyList<MacroConstantReportEntry> Entries,
    int DuplicateCoalescedCount);

internal static class MacroConstantMerger
{
    public static MacroConstantMergeResult Merge(IReadOnlyList<MacroValueClassification> classifications)
    {
        ArgumentNullException.ThrowIfNull(classifications);

        var constants = new List<BindingConstant>();
        var entries = new List<MacroConstantReportEntry>();
        var byName = new Dictionary<string, BindingConstant>(StringComparer.Ordinal);
        var coalesced = 0;

        foreach (var classification in classifications)
        {
            entries.Add(classification.Report);
            if (classification.Constant is null)
                continue;

            if (byName.TryGetValue(classification.Constant.Name, out var existing))
            {
                if (!IsCompatible(existing, classification.Constant))
                {
                    throw new InvalidOperationException(
                        $"Incompatible macro constant definitions for '{classification.Constant.Name}'. Existing value '{existing.Value}'/{existing.Type.ManagedName}; new value '{classification.Constant.Value}'/{classification.Constant.Type.ManagedName}.");
                }

                coalesced++;
                continue;
            }

            byName.Add(classification.Constant.Name, classification.Constant);
            constants.Add(classification.Constant);
        }

        return new MacroConstantMergeResult(
            constants.OrderBy(c => c.Name, StringComparer.Ordinal).ToList(),
            entries,
            coalesced);
    }

    private static bool IsCompatible(BindingConstant left, BindingConstant right) =>
        string.Equals(left.Name, right.Name, StringComparison.Ordinal)
        && string.Equals(left.Type.ManagedName, right.Type.ManagedName, StringComparison.Ordinal)
        && string.Equals(left.Value, right.Value, StringComparison.Ordinal)
        && left.Kind == right.Kind;
}
```

- [ ] **Step 4: Write failing manual policy tests**

Create `MacroManualPolicyApplierTests.cs`:

```csharp
using System.Collections.Immutable;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Translation;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class MacroManualPolicyApplierTests
{
    [Test]
    public async Task Apply_Should_Add_Required_Constant_When_Source_Does_Not_Provide_It()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig(requiredConstants:
        [
            BindingGenerationFixture.RequiredConstant("SDL_INIT_TIMER"),
        ]);

        var result = MacroManualPolicyApplier.Apply([], [], config);

        await Assert.That(result.Constants.Select(c => c.Name).ToArray()).IsEquivalentTo(["SDL_INIT_TIMER"]);
    }

    [Test]
    public async Task Apply_Should_Throw_When_Required_Constant_Is_Redundant_And_Not_Allowed_Stale()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig(requiredConstants:
        [
            BindingGenerationFixture.RequiredConstant("SDL_HINT_RENDER_DRIVER", type: "ReadOnlySpan<byte>", value: "\"SDL_RENDER_DRIVER\"u8", sourceHeader: "SDL_hints.h"),
        ]);
        var generated = Constant("SDL_HINT_RENDER_DRIVER", "ReadOnlySpan<byte>", "\"SDL_RENDER_DRIVER\"u8");

        await Assert.That(() => MacroManualPolicyApplier.Apply([generated], [], config))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("Manual required constant 'SDL_HINT_RENDER_DRIVER' is redundant");
    }

    [Test]
    public async Task Apply_Should_Remove_Excluded_Generated_Constant()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig() with
        {
            MacroConstants = new MacroConstantPolicyConfig
            {
                Excluded = ImmutableDictionary<string, ManualMacroConstantPolicyEntry>.Empty.Add(
                    "SDL_PRIVATE_HEADER_SWITCH",
                    new ManualMacroConstantPolicyEntry { Reason = "Not public API." }),
            },
        };

        var result = MacroManualPolicyApplier.Apply([Constant("SDL_PRIVATE_HEADER_SWITCH", "int", "1")], [], config);

        await Assert.That(result.Constants).IsEmpty();
        await Assert.That(result.ExcludedCount).IsEqualTo(1);
    }

    [Test]
    public async Task Apply_Should_Throw_When_Exclude_Is_Unused_And_Not_Allowed_Stale()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig() with
        {
            MacroConstants = new MacroConstantPolicyConfig
            {
                Excluded = ImmutableDictionary<string, ManualMacroConstantPolicyEntry>.Empty.Add(
                    "SDL_MISSING",
                    new ManualMacroConstantPolicyEntry { Reason = "Should be present." }),
            },
        };

        await Assert.That(() => MacroManualPolicyApplier.Apply([], [], config))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("Manual macro exclusion 'SDL_MISSING' was not consumed");
    }

    [Test]
    public async Task Apply_Should_Replace_Generated_Constant_With_Override()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig() with
        {
            MacroConstants = new MacroConstantPolicyConfig
            {
                Overrides = ImmutableDictionary<string, MacroConstantOverrideConfig>.Empty.Add(
                    "SDL_FIXTURE_OVERRIDE",
                    new MacroConstantOverrideConfig
                    {
                        Type = "uint",
                        Value = "42u",
                        SourceHeader = "SDL_fixture.h",
                        Kind = ConstantKind.Literal,
                        Reason = "Fixture override.",
                    }),
            },
        };

        var result = MacroManualPolicyApplier.Apply([Constant("SDL_FIXTURE_OVERRIDE", "uint", "1u")], [], config);

        await Assert.That(result.Constants.Single().Value).IsEqualTo("42u");
        await Assert.That(result.OverriddenCount).IsEqualTo(1);
    }

    private static BindingConstant Constant(string name, string type, string value) =>
        new(name, BindingGenerationFixture.NativePrimitive(type, type), value, ConstantKind.Literal);
}
```

- [ ] **Step 5: Run manual policy tests and verify they fail**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~MacroManualPolicyApplierTests"
```

Expected: FAIL because `MacroManualPolicyApplier` does not exist.

- [ ] **Step 6: Implement manual policy applier**

Create `MacroManualPolicyApplier.cs`:

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed record MacroManualPolicyResult(
    IReadOnlyList<BindingConstant> Constants,
    IReadOnlyList<MacroConstantReportEntry> Entries,
    int ExcludedCount,
    int OverriddenCount);

internal static class MacroManualPolicyApplier
{
    public static MacroManualPolicyResult Apply(
        IReadOnlyList<BindingConstant> generatedConstants,
        IReadOnlyList<MacroConstantReportEntry> entries,
        BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(generatedConstants);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(config);

        var constants = generatedConstants.ToDictionary(c => c.Name, StringComparer.Ordinal);
        var reportEntries = entries.ToList();
        var excludedCount = 0;
        var overriddenCount = 0;

        foreach (var required in config.RequiredConstants)
        {
            if (constants.ContainsKey(required.Name))
            {
                if (!required.AllowStale)
                    throw new InvalidOperationException($"Manual required constant '{required.Name}' is redundant because the macro is generated from headers.");
                reportEntries.Add(new MacroConstantReportEntry(required.Name, required.SourceHeader, "manual", "stale-include", required.Reason ?? "manual required constant kept as stale-tolerant", null, null));
                continue;
            }

            var translated = RequiredConstantTranslator.Translate([required]).Single();
            constants.Add(translated.Name, translated);
            reportEntries.Add(new MacroConstantReportEntry(required.Name, required.SourceHeader, "manual", "included", required.Reason ?? "manual required constant", translated.Type.ManagedName, translated.Value));
        }

        foreach (var exclusion in config.MacroConstants.Excluded)
        {
            if (!constants.Remove(exclusion.Key))
            {
                if (!exclusion.Value.AllowStale)
                    throw new InvalidOperationException($"Manual macro exclusion '{exclusion.Key}' was not consumed.");
                reportEntries.Add(new MacroConstantReportEntry(exclusion.Key, "manifest", "manual", "stale-exclude", exclusion.Value.Reason, null, null));
                continue;
            }

            excludedCount++;
            reportEntries.Add(new MacroConstantReportEntry(exclusion.Key, "manifest", "manual", "excluded", exclusion.Value.Reason, null, null));
        }

        foreach (var overrideEntry in config.MacroConstants.Overrides)
        {
            if (!constants.ContainsKey(overrideEntry.Key))
            {
                if (!overrideEntry.Value.AllowStale)
                    throw new InvalidOperationException($"Manual macro override '{overrideEntry.Key}' was not consumed.");
                reportEntries.Add(new MacroConstantReportEntry(overrideEntry.Key, overrideEntry.Value.SourceHeader, "manual", "stale-override", overrideEntry.Value.Reason, null, null));
                continue;
            }

            var replacementConfig = new RequiredConstantConfig
            {
                Name = overrideEntry.Key,
                Type = overrideEntry.Value.Type,
                Value = overrideEntry.Value.Value,
                SourceHeader = overrideEntry.Value.SourceHeader,
                Kind = overrideEntry.Value.Kind,
                Reason = overrideEntry.Value.Reason,
                AllowStale = overrideEntry.Value.AllowStale,
            };
            var replacement = RequiredConstantTranslator.Translate([replacementConfig]).Single();
            constants[overrideEntry.Key] = replacement;
            overriddenCount++;
            reportEntries.Add(new MacroConstantReportEntry(overrideEntry.Key, overrideEntry.Value.SourceHeader, "manual", "overridden", overrideEntry.Value.Reason, replacement.Type.ManagedName, replacement.Value));
        }

        return new MacroManualPolicyResult(
            constants.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ToList(),
            reportEntries,
            excludedCount,
            overriddenCount);
    }
}
```

- [ ] **Step 7: Run merge and manual policy tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~MacroConstantMergerTests|FullyQualifiedName~MacroManualPolicyApplierTests"
```

Expected: PASS.

- [ ] **Step 8: Commit approval checkpoint**

Show Deniz this proposed message and wait:

```text
feat: validate macro duplicate and manual policy rules
```

---

## Task 5: BindingConstantTranslator Integration

**Files:**
- Create: `build\_build\Targets\GenerateBindings\Translation\BindingConstantTranslator.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\BindingConstantTranslatorTests.cs`
- Modify: `build\_build\Targets\GenerateBindings\Model\BindingModel.cs:17-34`
- Modify: `build\_build\Targets\GenerateBindings\Model\BindingConstant.cs:5-20`
- Modify: `build\_build\Targets\GenerateBindings\Translation\CppAstToBindingModel.cs:23-75`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Translation\CppAstToBindingModelTests.cs:156-181`
- Modify: `build\_build.Tests\Scenarios\GenerateBindings\GenerateBindingsTaskScenarioTests.cs:176-185`

- [ ] **Step 1: Write failing translator tests**

Create `BindingConstantTranslatorTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Translation;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class BindingConstantTranslatorTests
{
    [Test]
    public async Task Translate_Should_Emit_Source_Header_String_And_Numeric_Macros()
    {
        var compilation = new CppCompilation();
        compilation.Macros.Add(Macro("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\"", "SDL_hints.h"));
        compilation.Macros.Add(Macro("SDL_BUTTON_LEFT", "1", "SDL_mouse.h"));

        var result = new BindingConstantTranslator().Translate(
            [new CppAstParseResult(ParseView("Neutral"), [compilation])],
            BindingGenerationFixture.Sdl2CoreConfig());

        await Assert.That(result.Constants.Select(c => c.Name).ToArray())
            .IsEquivalentTo(["SDL_BUTTON_LEFT", "SDL_HINT_RENDER_DRIVER"]);
        await Assert.That(result.Report.EmittedCount).IsEqualTo(2);
    }

    [Test]
    public async Task Translate_Should_Report_Function_Like_Macros_Without_Emitting()
    {
        var macro = Macro("SDL_BUTTON", "(1u << ((X) - 1))", "SDL_mouse.h");
        macro.Parameters = ["X"];
        var compilation = new CppCompilation();
        compilation.Macros.Add(macro);

        var result = new BindingConstantTranslator().Translate(
            [new CppAstParseResult(ParseView("Neutral"), [compilation])],
            BindingGenerationFixture.Sdl2CoreConfig());

        await Assert.That(result.Constants).IsEmpty();
        await Assert.That(result.Report.UnsupportedCount).IsEqualTo(1);
        await Assert.That(result.Report.Entries.Single().Reason).IsEqualTo("function-like macro");
    }

    private static PlatformParseView ParseView(string name) =>
        new(name, PlatformConditionKind.Neutral, SupportedOsPlatform: null, Defines: [], Undefines: []);

    private static CppMacro Macro(string name, string value, string header)
    {
        var macro = new CppMacro(name) { Value = value };
        macro.Span = new CppSourceSpan(
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", 0, 1, 1),
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{header}", 1, 1, 2));
        return macro;
    }
}
```

- [ ] **Step 2: Run translator tests and verify they fail**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingConstantTranslatorTests"
```

Expected: FAIL because `BindingConstantTranslator` does not exist.

- [ ] **Step 3: Add macro report property to BindingModel**

Modify `BindingModel.cs`:

```csharp
public MacroConstantReport MacroReport { get; init; } = MacroConstantReport.Empty;
```

Keep the existing constructors unchanged so current tests do not churn.

- [ ] **Step 4: Implement BindingConstantTranslator**

Create `BindingConstantTranslator.cs`:

```csharp
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed record BindingConstantTranslationResult(
    IReadOnlyList<BindingConstant> Constants,
    MacroConstantReport Report);

internal sealed class BindingConstantTranslator
{
    private readonly MacroCandidateCollector _collector = new();

    public BindingConstantTranslationResult Translate(
        IReadOnlyList<CppAstParseResult> parseResults,
        BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(parseResults);
        ArgumentNullException.ThrowIfNull(config);

        var rawCandidates = _collector.Collect(parseResults);
        var classifications = new List<MacroValueClassification>();
        var reportEntries = new List<MacroConstantReportEntry>();

        foreach (var candidate in rawCandidates)
        {
            var decision = MacroApiPolicy.Classify(candidate);
            if (decision.Disposition != MacroApiDisposition.Candidate)
            {
                reportEntries.Add(new MacroConstantReportEntry(
                    candidate.Name,
                    candidate.SourceHeader,
                    candidate.ParseViewName,
                    decision.Disposition == MacroApiDisposition.Unsupported ? "unsupported" : "skipped",
                    decision.Reason,
                    null,
                    null));
                continue;
            }

            classifications.Add(MacroValueClassifier.Classify(candidate));
        }

        var merged = MacroConstantMerger.Merge(classifications);
        var manual = MacroManualPolicyApplier.Apply(merged.Constants, [.. reportEntries, .. merged.Entries], config);
        var entries = manual.Entries;
        var report = new MacroConstantReport(
            ParsedCount: rawCandidates.Count,
            CandidateCount: classifications.Count,
            EmittedCount: manual.Constants.Count,
            SkippedCount: entries.Count(entry => entry.Disposition == "skipped"),
            ExcludedCount: manual.ExcludedCount,
            OverriddenCount: manual.OverriddenCount,
            DuplicateCoalescedCount: merged.DuplicateCoalescedCount,
            UnsupportedCount: entries.Count(entry => entry.Disposition == "unsupported"),
            ConflictCount: 0,
            Entries: entries.OrderBy(e => e.Name, StringComparer.Ordinal).ThenBy(e => e.ParseViewName, StringComparer.Ordinal).ToList());

        return new BindingConstantTranslationResult(manual.Constants, report);
    }
}
```

- [ ] **Step 5: Wire CppAstToBindingModel**

Replace:

```csharp
var constants = RequiredConstantTranslator.Translate(config.RequiredConstants);
```

with:

```csharp
var constantTranslation = new BindingConstantTranslator().Translate(parseResults, config);
var constants = constantTranslation.Constants;
```

Replace the return with:

```csharp
return new BindingModel(views, structs, enums, constants, handles, callbacks)
{
    MacroReport = constantTranslation.Report,
};
```

- [ ] **Step 6: Update CppAstToBindingModel required-constants test**

Keep `Translate_Should_Merge_RequiredConstants_Into_Model`, but assert the include source is now manual report evidence:

```csharp
await Assert.That(model.MacroReport.Entries.Select(entry => entry.Disposition).ToArray())
    .Contains("included");
```

Add a new test:

```csharp
[Test]
public async Task Translate_Should_Collect_Source_Macros_Into_Model_Constants()
{
    var compilation = new CppCompilation();
    compilation.Macros.Add(SdlMacro("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\"", "SDL_hints.h"));

    var model = CppAstToBindingModel.Translate(
        [ParseResult("Neutral", null, compilation)],
        DefaultConfig,
        NoRequired);

    var hint = model.Constants.Single(c => c.Name == "SDL_HINT_RENDER_DRIVER");
    await Assert.That(hint.Type.ManagedName).IsEqualTo("ReadOnlySpan<byte>");
    await Assert.That(hint.Value).IsEqualTo("\"SDL_RENDER_DRIVER\"u8");
}
```

Add helper near `SdlFunction`:

```csharp
private static CppMacro SdlMacro(string name, string value, string headerName) =>
    new(name)
    {
        Value = value,
        Span = SdlHeaderSpan(headerName),
    };
```

- [ ] **Step 7: Add scenario macro to semantic inventory**

In `GenerateBindingsTaskScenarioTests.SemanticInventoryCompilation`, add:

```csharp
compilation.Macros.Add(SdlMacro("SDL_HINT_RENDER_DRIVER", "\"SDL_RENDER_DRIVER\"", "SDL_hints.h"));
```

Add helper:

```csharp
private static CppMacro SdlMacro(string name, string value, string headerName) =>
    new(name)
    {
        Value = value,
        Span = SdlHeaderSpan(headerName),
    };
```

The existing scenario log assertion currently says `1 constants`; after the source macro is emitted alongside the required constant, update that assertion to:

```csharp
await Assert.That(result.Log.HasMessage(LogLevel.Information, "Model categories: 1 structs, 1 enums, 2 constants, 1 handles, 1 callbacks.")).IsTrue();
```

- [ ] **Step 8: Run integration-focused tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingConstantTranslatorTests|FullyQualifiedName~CppAstToBindingModelTests|FullyQualifiedName~GenerateBindingsTaskScenarioTests"
```

Expected: PASS.

- [ ] **Step 9: Commit approval checkpoint**

Show Deniz this proposed message and wait:

```text
feat: wire source-first macro constants into binding model
```

---

## Task 6: Macro Evidence in parse-views.json

**Files:**
- Modify: `build\_build\Targets\GenerateBindings\Emitting\BindingParseViewReport.cs:3-39`
- Modify: `build\_build\Targets\GenerateBindings\Emitting\CsCommandEmitter.cs:135-170`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\BindingModelData.cs:150-185`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\BindingParseViewReportTests.cs:8-79`
- Modify: `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\CsCommandEmitterTests.cs:172-225`

- [ ] **Step 1: Write failing parse report round-trip assertions**

In `BindingParseViewReportTests`, add a `MacroConstants` argument to the `BindingParseViewReport` construction:

```csharp
MacroConstants: new BindingMacroConstantsReport(
    ParsedCount: 3,
    CandidateCount: 2,
    EmittedCount: 1,
    SkippedCount: 1,
    ExcludedCount: 0,
    OverriddenCount: 0,
    DuplicateCoalescedCount: 1,
    UnsupportedCount: 1,
    ConflictCount: 0,
    Entries:
    [
        new BindingMacroConstantReportEntry(
            "SDL_HINT_RENDER_DRIVER",
            "SDL_hints.h",
            "Neutral",
            "emitted",
            "safe macro value",
            "ReadOnlySpan<byte>",
            "\"SDL_RENDER_DRIVER\"u8"),
    ])
```

Assert after deserialization:

```csharp
await Assert.That(round!.MacroConstants.ParsedCount).IsEqualTo(3);
await Assert.That(round.MacroConstants.Entries.Single().Name).IsEqualTo("SDL_HINT_RENDER_DRIVER");
await Assert.That(round.MacroConstants.Entries.Single().EmittedType).IsEqualTo("ReadOnlySpan<byte>");
```

- [ ] **Step 2: Run report model test and verify it fails**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingParseViewReportTests"
```

Expected: FAIL because the JSON report records have no macro report property.

- [ ] **Step 3: Extend report records**

Modify `BindingParseViewReport.cs`:

```csharp
internal sealed record BindingParseViewReport(
    int SchemaVersion,
    BindingParseViewReportCategories Categories,
    IReadOnlyList<string> EmittedFiles,
    IReadOnlyList<BindingParseViewReportEntry> Views,
    BindingMacroConstantsReport MacroConstants);
```

Add records:

```csharp
internal sealed record BindingMacroConstantsReport(
    int ParsedCount,
    int CandidateCount,
    int EmittedCount,
    int SkippedCount,
    int ExcludedCount,
    int OverriddenCount,
    int DuplicateCoalescedCount,
    int UnsupportedCount,
    int ConflictCount,
    IReadOnlyList<BindingMacroConstantReportEntry> Entries);

internal sealed record BindingMacroConstantReportEntry(
    string Name,
    string SourceHeader,
    string ParseViewName,
    string Disposition,
    string Reason,
    string? EmittedType,
    string? EmittedValue);
```

- [ ] **Step 4: Write failing emitter report assertions**

In `Emit_Should_Write_Rich_Parse_View_Audit_Report`, after view assertions add:

```csharp
var macroConstants = root.GetProperty("MacroConstants");
await Assert.That(macroConstants.GetProperty("ParsedCount").GetInt32()).IsEqualTo(1);
await Assert.That(macroConstants.GetProperty("EmittedCount").GetInt32()).IsEqualTo(1);
var macroEntry = macroConstants.GetProperty("Entries").EnumerateArray().Single();
await Assert.That(macroEntry.GetProperty("Name").GetString()).IsEqualTo("SDL_INIT_TIMER");
await Assert.That(macroEntry.GetProperty("Disposition").GetString()).IsEqualTo("included");
```

Update the `return new BindingModel(...)` expression in `BindingModelData.ModelWithRichParseViewEvidence()` to use an object initializer:

```csharp
            Callbacks:
            [
                new BindingCallback("SDL_AudioCallback", BindingGenerationFixture.NativeVoid(), []),
            ])
        {
            MacroReport = new MacroConstantReport(
                ParsedCount: 1,
                CandidateCount: 0,
                EmittedCount: 1,
                SkippedCount: 0,
                ExcludedCount: 0,
                OverriddenCount: 0,
                DuplicateCoalescedCount: 0,
                UnsupportedCount: 0,
                ConflictCount: 0,
                Entries:
                [
                    new MacroConstantReportEntry("SDL_INIT_TIMER", "SDL.h", "manual", "included", "manual required constant", "uint", "0x00000001u"),
                ]),
        };
```

- [ ] **Step 5: Wire report emission**

In `CsCommandEmitter.EmitReportJson`, map `model.MacroReport`:

```csharp
var macroReport = new BindingMacroConstantsReport(
    model.MacroReport.ParsedCount,
    model.MacroReport.CandidateCount,
    model.MacroReport.EmittedCount,
    model.MacroReport.SkippedCount,
    model.MacroReport.ExcludedCount,
    model.MacroReport.OverriddenCount,
    model.MacroReport.DuplicateCoalescedCount,
    model.MacroReport.UnsupportedCount,
    model.MacroReport.ConflictCount,
    [.. model.MacroReport.Entries.Select(entry => new BindingMacroConstantReportEntry(
        entry.Name,
        entry.SourceHeader,
        entry.ParseViewName,
        entry.Disposition,
        entry.Reason,
        entry.EmittedType,
        entry.EmittedValue))]);
```

Pass it to the `BindingParseViewReport` constructor:

```csharp
var report = new BindingParseViewReport(
    SchemaVersion: 1,
    Categories: categories,
    EmittedFiles: emittedFiles,
    Views: entries,
    MacroConstants: macroReport);
```

- [ ] **Step 6: Run report tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingParseViewReportTests|FullyQualifiedName~CsCommandEmitterTests"
```

Expected: PASS.

- [ ] **Step 7: Commit approval checkpoint**

Show Deniz this proposed message and wait:

```text
feat: report macro constant generation evidence
```

---

## Task 7: Live Manifest Cleanup and Real Generation Readiness

**Files:**
- Modify: `build\manifest.json:233-245`
- Modify: `build\_build.Tests\Fixtures\Data\Manifest\manifest-real.json:213-217`
- Modify: `build\_build.Tests\Unit\Data\BindingGeneration\BindingGenerationConfigRepositoryTests.cs:77-89`

- [ ] **Step 1: Write failing repository expectation for reduced manual constants**

Change the SDL2.Core config test expectations:

```csharp
await Assert.That(config.RequiredConstants.Count).IsEqualTo(2);
await Assert.That(config.RequiredConstants[0].Name).IsEqualTo("SDL_INIT_TIMER");
await Assert.That(config.RequiredConstants[1].Name).IsEqualTo("SDL_INIT_EVERYTHING");
await Assert.That(config.RequiredConstants.Select(c => c.Name).ToArray())
    .DoesNotContain("SDL_HINT_RENDER_DRIVER");
```

- [ ] **Step 2: Run repository test and verify it fails**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingGenerationConfigRepositoryRoundTripTests"
```

Expected: FAIL because fixture `manifest-real.json` still includes `SDL_HINT_RENDER_DRIVER`.

- [ ] **Step 3: Remove SDL_HINT_RENDER_DRIVER from manual required constants**

Remove this object from both `build\manifest.json` and `build\_build.Tests\Fixtures\Data\Manifest\manifest-real.json`:

```json
{ "name": "SDL_HINT_RENDER_DRIVER",  "type": "ReadOnlySpan<byte>", "value": "\"SDL_RENDER_DRIVER\"u8", "source_header": "SDL_hints.h", "kind": "Literal" }
```

Keep `SDL_INIT_*` entries because `SDL.h` is still excluded from the per-header parse loop.

- [ ] **Step 4: Run repository and translation tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingGenerationConfigRepositoryRoundTripTests|FullyQualifiedName~BindingConstantTranslatorTests|FullyQualifiedName~CppAstToBindingModelTests"
```

Expected: PASS.

- [ ] **Step 5: Run real binding generation**

Run:

```pwsh
dotnet run --file tools.cs -- generate-bindings
```

Expected: command succeeds and logs a model category line with a constant count greater than the previous manifest-only count of 10 SDL_INIT values. The exact count may vary with SDL headers, but `Constants.g.cs` must be emitted.

- [ ] **Step 6: Probe generated constants**

Run:

```pwsh
Select-String -Path artifacts\generated-bindings-preview\sdl2-core\Constants.g.cs -Pattern "SDL_HINT_RENDER_DRIVER","SDL_HINT_RENDER_VSYNC","public const uint SDL_INIT_TIMER"
```

Expected: output includes all three patterns. `SDL_HINT_RENDER_DRIVER` and `SDL_HINT_RENDER_VSYNC` must come from parsed headers, not `required_constants`.

- [ ] **Step 7: Inspect parse report macro evidence**

Run:

```pwsh
Select-String -Path artifacts\generated-bindings-preview\sdl2-core\parse-views.json -Pattern '"MacroConstants"','"SDL_HINT_RENDER_DRIVER"','"Disposition": "emitted"'
```

Expected: output includes the `MacroConstants` section, `SDL_HINT_RENDER_DRIVER`, and emitted macro evidence.

- [ ] **Step 8: Run compile-check**

Run:

```pwsh
dotnet build tests\binding-compile-check\SDL2.Core.CompileCheck.csproj -c Release
```

Expected: build succeeds across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, and `net462`.

- [ ] **Step 9: Commit approval checkpoint**

Show Deniz this proposed message and wait:

```text
feat: generate SDL hint constants from headers
```

---

## Task 8: Documentation and Final Verification

**Files:**
- Modify: `docs\binding-autogen\binding-api-surface-strategy.md:163-181`
- Modify: `docs\playbook\binding-generator-maintenance.md:107-139`
- Modify: `docs\plan.md`
- Modify: `C:\Users\deniz\.copilot\session-state\f5ef8536-6970-41b4-b5bc-bffcfbc179dc\plan.md`

- [ ] **Step 1: Update API surface strategy constants section**

In `docs\binding-autogen\binding-api-surface-strategy.md`, update the constants bullets to state:

```markdown
- source-visible object-like `SDL_*` macros are collected from parsed public SDL2.Core headers and classified before emission;
- `binding_generation.required_constants` is a manual include path for constants that are not source-visible through the per-header parse loop, not the primary source for normal header macros;
- manual macro excludes/overrides must be consumed or explicitly marked stale-tolerant, otherwise generation fails;
- function-like macros are reported as unsupported macro facts and are not emitted as constants;
```

Keep the existing UTF-8 span example for string-like macros.

- [ ] **Step 2: Update maintenance playbook**

In `docs\playbook\binding-generator-maintenance.md`, add a section after "Header Set Resolver Exclusions Maintenance":

```markdown
## Macro Constants Maintenance

Macro constants are source-first. The generator collects object-like `SDL_*` macros from parsed SDL2.Core public headers, classifies non-API/header-control macros, emits safe string and numeric constants, and writes macro evidence into `parse-views.json`.

Use `binding_generation.required_constants` only for constants that are not visible through the per-header parse loop, such as current `SDL_INIT_*` values from excluded `SDL.h`. Use `binding_generation.macro_constants.excluded` only when a generated source-visible macro is intentionally not public binding API. Use `binding_generation.macro_constants.overrides` only when the source-visible value needs an explicit managed shape.

Unused manual includes, excludes, and overrides fail by default. If a stale-tolerant entry is necessary, it must carry `allow_stale: true` and a reason that explains why the entry remains in the manifest.
```

- [ ] **Step 3: Update roadmap status**

In `docs\plan.md`, add the macro-generation milestone under the Phase 4 / SDL2.Core binding generator status:

```markdown
- SDL macro constants are source-first: object-like `SDL_*` macros from parsed public SDL2.Core headers flow through classifier/merge/manual-policy validation, with macro evidence in `parse-views.json`; `required_constants` remains only for source-invisible manual includes such as `SDL_INIT_*`.
```

Mirror the same status note in the session plan file.

- [ ] **Step 4: Run full build-host tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Expected: all non-skipped tests pass.

- [ ] **Step 5: Run generated compile-check**

Run:

```pwsh
dotnet build tests\binding-compile-check\SDL2.Core.CompileCheck.csproj -c Release
```

Expected: all target frameworks build successfully.

- [ ] **Step 6: Run slopwatch**

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

Expected: zero warnings/errors from slopwatch.

- [ ] **Step 7: Run whitespace diff check**

Run:

```pwsh
git diff --check
```

Expected: no whitespace errors. CRLF warnings may appear for existing Windows-normalized files; no new whitespace errors should appear.

- [ ] **Step 8: Request final code review**

Use the code-review agent with this prompt:

```text
Review the SDL macro constants implementation against docs\superpowers\specs\2026-05-19-sdl-macro-constants-design.md and docs\superpowers\plans\2026-05-19-sdl-macro-constants-implementation-plan.md. Focus only on real correctness issues: source-first macro collection, stale manual policy enforcement, duplicate conflict handling, parse-views report evidence, generated constant compile safety, and unintended public API leaks.
```

Expected: reviewer either approves or reports actionable correctness issues. Address any important findings with RED/GREEN tests before finalizing.

- [ ] **Step 9: Final approval checkpoint**

Show Deniz a summary of changed files, verification outputs, and this proposed conventional commit message. Wait for explicit approval before committing:

```text
feat: auto-generate SDL macro constants
```
