# Milestone 1 Safety Harness And Baseline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the snapshot, fixture, and compile/smoke baseline that lets later `GenerateBindings` refactors prove behavior preservation instead of relying on vibes.

**Architecture:** Keep this milestone test-first and behavior-preserving. Add snapshot gates around generated file sets and fake task orchestration, expand embedded `.h` fixture integration coverage, and add an opt-in real generated-preview snapshot gate for milestone checkpoints. Do not refactor production topology in this milestone; prepare the safety rails for Milestone 2.

**Tech Stack:** .NET 10, TUnit, Microsoft.Testing.Platform, CppAst fixture parsing, Verify.TUnit snapshots, Cake `FakeCakeWorld`, `tools.cs generate-bindings`, `tests/binding-compile-check/SDL2.Core.CompileCheck.csproj`.

---

## Non-Negotiables

- Do not change production generator behavior unless a RED test exposes a real bug and Deniz approves the behavior change.
- Use `git mv` for every tracked file move/rename.
- Use `dotnet add package` for NuGet package changes. Do not edit package XML by hand.
- Do not commit without explicit approval. At commit points, stop and present a summary plus proposed commit message.
- Keep normal build-host test runs native-free. Docker/libclang/vcpkg-generated preview checks are milestone gates, not inner-loop unit tests.
- Snapshot changes are reviewed artifacts. Commit `*.verified.*`; never commit `*.received.*`.

## Implementation Notes

- `Verify.TUnit` 31.16.x requires TUnit core/engine 1.43.x; M1 aligns the central `TUnit` package version to avoid mixed TUnit runtime assemblies.
- Verify's transitive `global using VerifyTests` collides with the repository's `Build.Tests.Fixtures.TempDirectory`; M1 removes that transitive global using in `Build.Tests.csproj` and imports `VerifyTests` only where needed.
- Current Verify path configuration uses `Verifier.UseProjectRelativeDirectory(...)`; older `VerifyBase` examples are stale for the installed package version.
- Microsoft Testing Platform returns exit code `8` when a filtered run selects only skipped tests. On Windows, use `--ignore-exit-code 8` for Linux-only or opt-in skip-only filtered verification commands.
- Linux-only semantic snapshots can be approved from Windows by running the filtered test inside `mcr.microsoft.com/dotnet/sdk:10.0.203` with the repository mounted at `/workspace`.

## File Structure

Planned files and responsibilities:

- Modify: `build/_build.Tests/Build.Tests.csproj`
  - Adds Verify snapshot package references through `dotnet add package`.
- Modify: `Directory.Packages.props`
  - Receives package versions through `dotnet add package` because CPM is enabled.
- Modify: `.gitignore`
  - Ignores Verify `.received` files.
- Modify: `.gitattributes`
  - Pins Verify snapshot text line endings.
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Snapshots/VerifyConfiguration.cs`
  - Central Verify initialization for generator snapshot tests.
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Snapshots/VerifyConventionsTests.cs`
  - Ensures Verify conventions stay valid.
- Create: `build/_build.Tests/Fixtures/GenerateBindings/SemanticFixtureParser.cs`
  - Shared parser/translator helper for embedded `.h` fixtures.
- Modify: `build/_build.Tests/Unit/Targets/GenerateBindings/Translation/SemanticHeaderFixtureTests.cs`
  - Reuses the shared parser helper only after snapshot rails are in place.
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/GeneratedFileSetSnapshotTests.cs`
  - Snapshots deterministic generated files from `BindingModelData.ModelWithRichParseViewEvidence()`.
- Create: `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskSnapshotTests.cs`
  - Snapshots fake end-to-end task output through `FakeCakeWorld`.
- Create: `build/_build.Tests/Fixtures/GeneratedPreviewSnapshotAttribute.cs`
  - Skips real generated-preview snapshot tests unless explicitly enabled.
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/GeneratedPreview/GeneratedPreviewSnapshotTests.cs`
  - Opt-in snapshot over `artifacts/generated-bindings-preview/sdl2-core` file inventory and hashes.
- Create: `build/_build.Tests/Fixtures/Data/GenerateBindings/SemanticTypes/platform-conditioned-functions.h`
  - Fixture for Neutral/platform function split behavior.
- Create: `build/_build.Tests/Fixtures/Data/GenerateBindings/MacroConstants/manual-policy-surface.h`
  - Fixture for manual macro include/exclude/override snapshot coverage.
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Translation/SemanticModelSnapshotTests.cs`
  - Snapshots compact semantic model projections from embedded `.h` fixtures.
- Modify: `docs/binding-autogen/testing-strategy.md`
  - Records the accepted M1 snapshot/fixture conventions after implementation.
- Modify: `docs/binding-autogen/binding-generator-roadmap.md`
  - Marks the M1 plan as the detailed execution reference.

## Task 1: Baseline Verification Before New Tests

**Files:**
- Read: `build/_build.Tests/Build.Tests.csproj`
- Read: `build/_build.Tests/Unit/Targets/GenerateBindings/**`
- Read: `build/_build.Tests/Fixtures/Data/GenerateBindings/**`

- [ ] **Step 1: Inspect worktree before touching tests**

Run:

```pwsh
git status --short
```

Expected: only the documentation changes already approved by Deniz, or a clean tree if those docs have been committed before implementation starts. If unrelated user changes exist, leave them alone.

- [ ] **Step 2: Run the current build-host regression suite**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: PASS. If it fails, stop and diagnose with the systematic-debugging skill before adding snapshot infrastructure.

- [ ] **Step 3: Run current generated preview when Docker is provisioned**

Run:

```pwsh
dotnet run --file tools.cs -- generate-bindings
```

Expected: `artifacts/generated-bindings-preview/sdl2-core/` contains `.g.cs` files and `parse-views.json`. If Docker/native prerequisites are missing, record the exact failure and continue with native-free Tasks 2-5; the opt-in generated-preview snapshot is completed when the provisioned environment is available.

- [ ] **Step 4: Run compile-check if generated preview exists**

Run:

```pwsh
dotnet build tests/binding-compile-check/SDL2.Core.CompileCheck.csproj -c Release
```

Expected: PASS when generated preview exists. If preview is absent, the compile-check should fail with `BINDING-COMPILE-CHECK-001`; that is an environment/baseline issue, not a generator refactor issue.

## Task 2: Add Verify Snapshot Infrastructure

**Files:**
- Modify: `build/_build.Tests/Build.Tests.csproj`
- Modify: `Directory.Packages.props`
- Modify: `.gitignore`
- Modify: `.gitattributes`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Snapshots/VerifyConfiguration.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Snapshots/VerifyConventionsTests.cs`

- [ ] **Step 1: Add Verify packages using CLI**

Run:

```pwsh
dotnet add build/_build.Tests/Build.Tests.csproj package Verify.TUnit
dotnet add build/_build.Tests/Build.Tests.csproj package Verify.DiffPlex
```

Expected: `Build.Tests.csproj`, `Directory.Packages.props`, and `build/_build.Tests/packages.lock.json` update through the CLI. Do not hand-edit package XML.

- [ ] **Step 2: Add Verify source-control conventions**

Patch `.gitignore` with:

```gitignore
# Verify snapshots: commit verified baselines, never transient received files.
*.received.*
*.received/
```

Patch `.gitattributes` with:

```gitattributes
# Verify snapshot baselines are reviewed generated text; keep diffs stable.
*.verified.txt text eol=lf working-tree-encoding=UTF-8
*.verified.json text eol=lf
*.verified.md text eol=lf
```

- [ ] **Step 3: Add Verify configuration**

Create `build/_build.Tests/Unit/Targets/GenerateBindings/Snapshots/VerifyConfiguration.cs`:

```csharp
using System.Runtime.CompilerServices;
using VerifyTests;

namespace Build.Tests.Unit.Targets.GenerateBindings.Snapshots;

internal static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Verifier.UseProjectRelativeDirectory("Unit/Targets/GenerateBindings/Snapshots");
    }
}
```

- [ ] **Step 4: Add Verify conventions test**

Create `build/_build.Tests/Unit/Targets/GenerateBindings/Snapshots/VerifyConventionsTests.cs`:

```csharp
namespace Build.Tests.Unit.Targets.GenerateBindings.Snapshots;

public sealed class VerifyConventionsTests
{
    [Test]
    public Task VerifyConventions_Should_Match_Project_Settings()
    {
        return VerifyChecks.Run();
    }
}
```

- [ ] **Step 5: Run conventions test and approve baseline**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/VerifyConventionsTests/*"
```

Expected first run: FAIL if Verify creates `.received` convention output or finds missing conventions. Fix source-control convention gaps, approve the verified baseline, then rerun.

Expected final run: PASS.

## Task 3: Extract Shared Semantic Fixture Parser

**Files:**
- Create: `build/_build.Tests/Fixtures/GenerateBindings/SemanticFixtureParser.cs`
- Modify: `build/_build.Tests/Unit/Targets/GenerateBindings/Translation/SemanticHeaderFixtureTests.cs`

- [ ] **Step 1: Add the shared helper without removing existing private helpers yet**

Create `build/_build.Tests/Fixtures/GenerateBindings/SemanticFixtureParser.cs`:

```csharp
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Translation;
using CppAst;

namespace Build.Tests.Fixtures.GenerateBindings;

internal static class SemanticFixtureParser
{
    public static BindingModel TranslateSdl2CoreFixture(
        string fixturePath,
        bool parseMacros = false,
        IReadOnlyList<BindingFunction>? requiredFunctions = null,
        IReadOnlyList<string>? defines = null,
        IReadOnlyList<string>? undefines = null)
    {
        var compilation = ParseFixture(fixturePath, parseMacros, parseAsSdl2Header: true, defines, undefines);
        return CppAstToBindingModel.Translate(
            [ParseResult("Neutral", compilation, defines, undefines)],
            BindingGenerationFixture.Sdl2CoreConfig(),
            requiredFunctions ?? []);
    }

    public static CppCompilation ParseFixture(
        string fixturePath,
        bool parseMacros = false,
        bool parseAsSdl2Header = false,
        IReadOnlyList<string>? defines = null,
        IReadOnlyList<string>? undefines = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "janset-semantic-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var headerDirectory = parseAsSdl2Header
            ? Path.Combine(directory, "include", "SDL2")
            : directory;
        Directory.CreateDirectory(headerDirectory);
        var header = Path.Combine(headerDirectory, Path.GetFileName(fixturePath));

        try
        {
            File.WriteAllText(header, FixtureLoader.Load(fixturePath));
            var options = new CppParserOptions
            {
                ParserKind = CppParserKind.C,
                TargetSystem = "linux",
                ParseMacros = parseMacros,
            };
            options.Defines.AddRange(defines ?? []);
            foreach (var undefine in undefines ?? [])
            {
                options.AdditionalArguments.Add($"-U{undefine}");
            }

            var compilation = CppParser.ParseFile(header, options);
            if (compilation.HasErrors)
            {
                throw new InvalidOperationException(compilation.Diagnostics.ToString());
            }

            return compilation;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    public static CppAstParseResult ParseResult(
        string viewName,
        CppCompilation compilation,
        IReadOnlyList<string>? defines = null,
        IReadOnlyList<string>? undefines = null)
    {
        return new CppAstParseResult(
            new PlatformParseView(
                Name: viewName,
                Kind: string.Equals(viewName, "Neutral", StringComparison.Ordinal)
                    ? PlatformConditionKind.Neutral
                    : PlatformConditionKind.OperatingSystem,
                SupportedOsPlatform: string.Equals(viewName, "Neutral", StringComparison.Ordinal) ? null : viewName.ToLowerInvariant(),
                Defines: defines ?? [],
                Undefines: undefines ?? []),
            [compilation]);
    }
}
```

- [ ] **Step 2: Verify helper compiles**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticHeaderFixtureTests/*"
```

Expected: same PASS/SKIP behavior as before. On non-Linux hosts, `[LinuxOnly]` tests skip.

- [ ] **Step 3: Replace private helper calls in `SemanticHeaderFixtureTests` with shared helper**

Update the test file to import:

```csharp
using Build.Tests.Fixtures.GenerateBindings;
```

Replace private calls so tests use:

```csharp
var compilation = SemanticFixtureParser.ParseFixture("GenerateBindings/SemanticTypes/opaque-and-concrete-structs.h");
var model = SemanticFixtureParser.TranslateSdl2CoreFixture("GenerateBindings/SemanticTypes/opaque-handle-aliases.h");
```

Delete the now-unused private `ParseFixture`, `TranslateFixture`, and `ParseResult` helpers from `SemanticHeaderFixtureTests.cs`.

- [ ] **Step 4: Run fixture tests again**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticHeaderFixtureTests/*"
```

Expected: PASS/SKIP with no changed assertions. This is a test-helper refactor only.

## Task 4: Snapshot Deterministic Generated File Sets

**Files:**
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/GeneratedFileSetSnapshotTests.cs`
- Existing helper: `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/BindingModelData.cs`

- [ ] **Step 1: Write the snapshot test**

Create `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/GeneratedFileSetSnapshotTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Emitting;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emitting;

public sealed class GeneratedFileSetSnapshotTests
{
    [Test]
    public Task Emit_Should_Match_Rich_Model_Generated_File_Set()
    {
        var fileSet = CsCommandEmitter.Emit(
            BindingModelData.ModelWithRichParseViewEvidence(),
            new BindingEmissionOptions("SDL2", "SDL"));

        var snapshot = fileSet.Files
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file => new GeneratedFileSnapshot(file.RelativePath, file.Content.Replace("\r\n", "\n", StringComparison.Ordinal)))
            .ToArray();

        return Verify(snapshot);
    }

    private sealed record GeneratedFileSnapshot(string RelativePath, string Content);
}
```

- [ ] **Step 2: Run RED and approve snapshot**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GeneratedFileSetSnapshotTests/*"
```

Expected first run: FAIL with a `.received` snapshot because no verified baseline exists.

Review the received snapshot. It should contain the existing file layout and generated content for `Constants.g.cs`, `Types/*.g.cs`, `Platform/*/Commands.g.cs`, and `parse-views.json`. Approve it as `.verified.txt`.

- [ ] **Step 3: Run GREEN**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GeneratedFileSetSnapshotTests/*"
```

Expected final run: PASS.

## Task 5: Snapshot Fake Task Orchestration Output

**Files:**
- Create: `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskSnapshotTests.cs`
- Reference: `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskScenarioTests.cs`

- [ ] **Step 1: Write a fake end-to-end snapshot test**

Create `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskSnapshotTests.cs` by reusing the existing scenario setup pattern. The test should seed a Linux fake world, seed one fake SDL2 header, substitute `ICppAstParseRunner` with `SemanticInventoryCompilation()`, run the task, and snapshot the emitted files plus relevant log messages.

Use this snapshot projection shape:

```csharp
private sealed record TaskOutputSnapshot(
    IReadOnlyList<string> Files,
    IReadOnlyDictionary<string, string> FileContents,
    IReadOnlyList<string> InformationMessages);
```

The test method name must follow repo convention:

```csharp
[Test]
public Task RunAsync_Should_Match_Semantic_Inventory_Output_Snapshot()
```

The snapshot should include these relative files:

```csharp
var generatedRoot = "artifacts/generated-bindings-preview/sdl2-core";
var files = new[]
{
    "Constants.g.cs",
    "Types/Enums.g.cs",
    "Types/Handles.g.cs",
    "Types/Structs.g.cs",
    "Types/Callbacks.g.cs",
    "Platform/Neutral/Commands.g.cs",
    "parse-views.json",
};
```

This list is the minimum expected inventory. The test should snapshot every discovered emitted file so future emitters cannot silently add or drop files outside the expected list.

- [ ] **Step 2: Run RED and approve snapshot**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskSnapshotTests/*"
```

Expected first run: FAIL with a `.received` snapshot because the verified baseline does not exist. Review that the snapshot pins orchestration output, not production implementation internals. Approve the snapshot.

- [ ] **Step 3: Run GREEN**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GenerateBindingsTaskSnapshotTests/*"
```

Expected final run: PASS.

## Task 6: Add Semantic Model Snapshot Fixtures

**Files:**
- Create: `build/_build.Tests/Fixtures/Data/GenerateBindings/SemanticTypes/platform-conditioned-functions.h`
- Create: `build/_build.Tests/Fixtures/Data/GenerateBindings/MacroConstants/manual-policy-surface.h`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Translation/SemanticModelSnapshotTests.cs`

- [ ] **Step 1: Add platform-conditioned function fixture**

Create `build/_build.Tests/Fixtures/Data/GenerateBindings/SemanticTypes/platform-conditioned-functions.h`:

```c
typedef unsigned int Uint32;

extern int SDL_AlwaysAvailable(Uint32 flags);

#if defined(JANSET_TEST_LINUX_VIEW)
extern int SDL_LinuxOnly(Uint32 flags);
#endif

#if defined(JANSET_TEST_WINDOWS_VIEW)
extern int SDL_WindowsOnly(Uint32 flags);
#endif
```

- [ ] **Step 2: Add manual macro policy fixture**

Create `build/_build.Tests/Fixtures/Data/GenerateBindings/MacroConstants/manual-policy-surface.h`:

```c
#define SDL_HINT_TEST_ALPHA "SDL_TEST_ALPHA"
#define SDL_HINT_TEST_BETA "SDL_TEST_BETA"
#define SDL_FIXTURE_OVERRIDE 1u
#define SDL_ASSERT_LEVEL 1
#define SDL_BUTTON(X) (1u << ((X)-1))
#define SDL_TEST_COMPUTED_FLAG (1u << 4)
```

- [ ] **Step 3: Write semantic model snapshot tests**

Create `build/_build.Tests/Unit/Targets/GenerateBindings/Translation/SemanticModelSnapshotTests.cs`:

```csharp
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
            Constants = result.Constants.Select(c => new { c.Name, Type = c.Type.ManagedName, c.Value, c.Kind }).OrderBy(c => c.Name),
            Report = result.Report.Entries.Select(e => new { e.Name, e.Disposition, e.Reason, e.MacroForm, e.Taxonomy }).OrderBy(e => e.Name),
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
                Parameters = function.Parameters.Select(parameter => new { parameter.Name, Type = parameter.Type.ManagedName }),
            }),
        }),
        Structs = model.Structs.Select(s => s.Name),
        Enums = model.Enums.Select(e => e.Name),
        Handles = model.Handles.Select(h => h.Name),
        Callbacks = model.Callbacks.Select(c => c.Name),
    };
}
```

- [ ] **Step 4: Fix compile issues only after RED**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticModelSnapshotTests/*"
```

Expected first run: FAIL because verified snapshots are missing, or compile fails if helper API names need exact adjustment. Fix compile issues without changing production behavior, rerun to produce `.received`, review, and approve.

- [ ] **Step 5: Run GREEN**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/SemanticModelSnapshotTests/*"
```

Expected final run: PASS on Linux, SKIP on non-Linux if `[LinuxOnly]` applies to the class.

## Task 7: Add Opt-In Real Generated Preview Snapshot Gate

**Files:**
- Create: `build/_build.Tests/Fixtures/GeneratedPreviewSnapshotAttribute.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/GeneratedPreview/GeneratedPreviewSnapshotTests.cs`

- [ ] **Step 1: Add opt-in skip attribute**

Create `build/_build.Tests/Fixtures/GeneratedPreviewSnapshotAttribute.cs`:

```csharp
namespace Build.Tests.Fixtures;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class GeneratedPreviewSnapshotAttribute : SkipAttribute
{
    public GeneratedPreviewSnapshotAttribute()
        : base("Generated preview snapshot tests require JANSET_VERIFY_GENERATED_PREVIEW=1")
    {
    }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
        return Task.FromResult(!string.Equals(
            Environment.GetEnvironmentVariable("JANSET_VERIFY_GENERATED_PREVIEW"),
            "1",
            StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Add generated preview snapshot test**

Create `build/_build.Tests/Unit/Targets/GenerateBindings/GeneratedPreview/GeneratedPreviewSnapshotTests.cs`:

```csharp
using System.Security.Cryptography;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.GeneratedPreview;

[GeneratedPreviewSnapshot]
public sealed class GeneratedPreviewSnapshotTests
{
    [Test]
    public Task GeneratedPreview_Should_Match_File_Inventory_Snapshot()
    {
        var root = FindRepositoryRoot();
        var previewRoot = Path.Combine(root, "artifacts", "generated-bindings-preview", "sdl2-core");
        if (!Directory.Exists(previewRoot))
        {
            throw new DirectoryNotFoundException("Generated preview directory not found. Run `dotnet run --file tools.cs -- generate-bindings` first.");
        }

        var entries = Directory.EnumerateFiles(previewRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".g.cs", StringComparison.Ordinal) || path.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => SnapshotEntry(previewRoot, path))
            .ToArray();

        return Verify(entries);
    }

    private static GeneratedPreviewFile SnapshotEntry(string previewRoot, string path)
    {
        var bytes = File.ReadAllBytes(path);
        var relative = Path.GetRelativePath(previewRoot, path).Replace('\\', '/');
        return new GeneratedPreviewFile(relative, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools.cs")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found from test output directory.");
    }

    private sealed record GeneratedPreviewFile(string RelativePath, long Length, string Sha256);
}
```

- [ ] **Step 3: Run skipped by default**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GeneratedPreviewSnapshotTests/*"
```

Expected: SKIP because `JANSET_VERIFY_GENERATED_PREVIEW` is not set.

- [ ] **Step 4: Generate preview and run opt-in RED**

Run:

```pwsh
dotnet run --file tools.cs -- generate-bindings
$env:JANSET_VERIFY_GENERATED_PREVIEW = "1"
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 -- --treenode-filter "/*/*/GeneratedPreviewSnapshotTests/*"
Remove-Item Env:\JANSET_VERIFY_GENERATED_PREVIEW
```

Expected first opt-in run: FAIL with a `.received` file because no verified preview inventory exists. Review the generated file inventory and hashes, then approve the snapshot.

- [ ] **Step 5: Run opt-in GREEN**

Run the same opt-in test again.

Expected final run: PASS.

## Task 8: Milestone Verification Commands

**Files:**
- All files touched in Tasks 2-7.

- [ ] **Step 1: Run the build-host regression suite**

Run:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

Expected: PASS, with generated-preview snapshot test skipped unless the environment variable is set.

- [ ] **Step 2: Run generator checkpoint when Docker is available**

Run:

```pwsh
dotnet run --file tools.cs -- generate-bindings
```

Expected: generated SDL2.Core preview exists and matches the approved generated-preview snapshot after the opt-in test is run.

- [ ] **Step 3: Run compile-check**

Run:

```pwsh
dotnet build tests/binding-compile-check/SDL2.Core.CompileCheck.csproj -c Release
```

Expected: PASS if generated preview exists.

- [ ] **Step 4: Run diff hygiene**

Run:

```pwsh
git --no-pager diff --check
```

Expected: no output.

- [ ] **Step 5: Run Slopwatch because code/test/project files changed**

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

Expected: PASS with no warnings. If this fails because Slopwatch is not installed, stop and report the missing tool instead of claiming the milestone is complete.

## Task 9: Update Docs After Implementation

**Files:**
- Modify: `docs/binding-autogen/testing-strategy.md`
- Modify: `docs/binding-autogen/binding-generator-roadmap.md`
- Modify: this plan if implementation discovers necessary sequencing changes.

- [ ] **Step 1: Record accepted snapshot conventions**

Update `docs/binding-autogen/testing-strategy.md` with the concrete snapshot locations:

```text
build/_build.Tests/Unit/Targets/GenerateBindings/Snapshots/
build/_build.Tests/Unit/Targets/GenerateBindings/GeneratedPreview/
```

Mention that generated-preview snapshot tests are opt-in through `JANSET_VERIFY_GENERATED_PREVIEW=1`.

- [ ] **Step 2: Add M1 completion evidence to roadmap**

Update `docs/binding-autogen/binding-generator-roadmap.md` Milestone 1 exit evidence with the actual verification commands run and their outcome. Keep it concise; do not paste full logs.

- [ ] **Step 3: Run docs diff check**

Run:

```pwsh
git --no-pager diff --check
```

Expected: no output.

## Task 10: Review And Commit Gate

**Files:**
- All touched files.

- [ ] **Step 1: Inspect final diff**

Run:

```pwsh
git status --short
git --no-pager diff --stat
git --no-pager diff -- build/_build.Tests build/_build.Tests/Build.Tests.csproj Directory.Packages.props .gitignore .gitattributes docs/binding-autogen
```

Expected: only intentional M1 safety harness, snapshot, fixture, and documentation changes.

- [ ] **Step 2: Request review before commit**

Use a read-only review agent with this scope:

```text
Review Milestone 1 safety harness changes. Focus on snapshot stability, TDD/RED-GREEN compliance, accidental production behavior changes, Verify convention issues, test flakiness, and whether generated-preview snapshot is safely opt-in.
```

Fix Critical and Important findings before proceeding.

- [ ] **Step 3: Ask Deniz for commit approval**

Present:

```text
Summary:
- Added Verify snapshot infrastructure for GenerateBindings tests.
- Added deterministic generated file set and fake task orchestration snapshots.
- Added semantic fixture snapshots and opt-in generated-preview inventory snapshot.
- Updated binding-autogen testing docs.

Verification:
- dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0: PASS
- dotnet run --file tools.cs -- generate-bindings: PASS or documented unavailable
- dotnet build tests/binding-compile-check/SDL2.Core.CompileCheck.csproj -c Release: PASS or documented unavailable because preview absent
- git --no-pager diff --check: PASS
- slopwatch analyze ...: PASS

Proposed commit message:
test(generate-bindings): add safety harness baseline snapshots
```

Do not commit until Deniz explicitly approves.

## Self-Review Checklist

- The plan covers M1 safety harness, snapshots, `.h` fixture integration, fake task orchestration, generated-preview checkpoint, compile-check, smoke/generator checkpoint, docs, and review gate.
- Package additions use `dotnet add package`, not hand-edited XML.
- No production `GenerateBindings` behavior changes are required by this milestone.
- The real generated-preview snapshot is opt-in and cannot make ordinary test runs depend on Docker or generated artifacts.
- The next milestone can use these snapshots to prove behavior-preserving topology refactors.
