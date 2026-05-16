# Binding Generator Local Output Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Linux-canonical Docker-based local generation loop that produces spike-style placeholder SDL2.Core binding output under `artifacts/generated-bindings-preview/sdl2-core/`, giving Stage 1 Tasks 4-6 (model + emitters) a real artifact to iterate against.

**Architecture:** New Cake target `GenerateBindings` (depends on existing `EnsureVcpkgDependencies`) inside `build/_build/Targets/GenerateBindings/`. Pure model + emitter under `Model/` and `Emitting/` sub-namespaces; Cake-aware orchestration in `GenerateBindingsTask` + `BindingGenerationRunner`. New derived Docker image `binding-generator.Dockerfile` (from `linux-builder`, adds .NET SDK + NuGet restore + source COPY). New `tools.cs generate-bindings` subcommand orchestrates `docker pull → inspect → build → volume → run` via CliWrap. Output bind-mounted to host scratch; vcpkg binary cache in a named Docker volume.

**Tech Stack:** .NET 10 / C# 14, CppAst 0.24.0, libclang.runtime.linux-x64 + libClangSharp.runtime.linux-x64 20.1.2, Cake Frosting 6.1, TUnit + Microsoft.Testing.Platform, Spectre.Console.Cli + CliWrap (in tools.cs), Docker (hard prerequisite for local invocation).

**Reference spec:** [`docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md`](../specs/2026-05-15-binding-generator-local-output-loop-design.md). The spec documents design rationale; this plan documents implementation steps.

**Approval discipline:** Per `AGENTS.md` §"Approval Gate", do NOT commit without explicit Deniz approval. Each "Commit" step in this plan means: present the summary + proposed message to Deniz, wait for "go / apply / proceed / başla / yap", then commit. Tasks 1-3 of the parent Stage 1 plan are still dirty in the working tree; commit boundary for this slice is the maintainer's call (single combined commit vs. two-commit split).

**Implementation status (2026-05-16):** Tasks 1-11 complete. Smoke produces 9 files, 848 Neutral / 51 platform-only delta. Task 11.5 (AST inline filter + `-U__has_builtin` restore + dynapi cross-check validator) added below — Stage 1 production frictions uncovered work that did not fit the 2026-05-15 plan shape. See [`../../binding-autogen/research/binding-autogen-spike-findings.md`](../../binding-autogen/research/binding-autogen-spike-findings.md) §11 for the full friction log and retracted assumptions.

---

## File Structure

### New files

```text
build/_build/Targets/GenerateBindings/
├── GenerateBindingsTask.cs                          (Cake task, [TaskName("GenerateBindings")])
├── BindingGenerationRunner.cs                       (Cake-aware orchestrator + IBindingGenerationRunner)
├── Model/                                            (PURE — no Cake)
│   ├── PreviewBindingModel.cs
│   ├── PreviewParseView.cs
│   ├── PreviewFunction.cs
│   ├── PreviewParameter.cs
│   └── CppAstToPreviewModel.cs
└── Emitting/                                          (PURE — no Cake)
    ├── GeneratedFile.cs
    ├── GeneratedFileSet.cs
    ├── PreviewParseViewReport.cs
    └── PreviewEmitter.cs

docker/
└── binding-generator.Dockerfile

build/_build.Tests/Unit/Targets/GenerateBindings/
└── Emitting/
    ├── PreviewEmitterTests.cs
    ├── PreviewParseViewReportTests.cs
    └── PreviewBindingModelData.cs                    (centralized inline test data)

build/_build.Tests/Scenarios/GenerateBindings/
└── GenerateBindingsTaskScenarioTests.cs
```

### Modified files

```text
build/_build/Host/Paths/PathService.cs                (IPathService + impl: two temp preview-root properties)
build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs   (extend AddGenerateBindings)
build/_build/Targets/GenerateBindings/GenerateBindingsRequest.cs        (re-shape if needed)
build/_build/Program.cs                                                 (already calls AddGenerateBindings — verify)
build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs  (assert AddGenerateBindings resolves)

tools.cs                                              (add GenerateBindingsCommand)
.dockerignore                                         (extend for binding-generator build context)
.gitignore                                            (add artifacts/generated-bindings-preview/)

docs/binding-autogen/binding-autogen-strategy-brief.md    (sequencing note)
docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md  (sequencing note)
docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md   (insert precursor task ref)
docs/playbook/binding-generator-maintenance.md            (trio table + post-Stage-1 revalidation + tools.cs loop)
docs/binding-autogen/README.md                            (Stage 1 status block)
```

> **2026-05-16 note:** Earlier drafts of this plan also touched `docs/parking-lot.md` for "deferred follow-ups." That was incorrect placement — parking-lot is reserved for items outside the binding-autogen package scope. Binding-autogen-internal deferrals belong in this slice's design spec §10 (canonical) and in [`2026-05-14-sdl2-core-binding-generator-stage-1.md`](2026-05-14-sdl2-core-binding-generator-stage-1.md) (Stage-1-staged deferrals). Task 12 below has been updated to drop the parking-lot step.

---

## Task 1: Add Temporary PathService Preview-Root Properties

**Files:**
- Modify: `build/_build/Host/Paths/PathService.cs`
- Test: `build/_build.Tests/Unit/Host/Paths/PathServiceTests.cs` (if missing, create new)

PathService gets two `IPathService` additions tagged for retirement when Task 5/7 of Stage 1 lands real emitters + production-location flag-flip. See spec §4.2.

- [ ] **Step 1: Check if PathServiceTests exists**

Run: `Test-Path build/_build.Tests/Unit/Host/Paths/PathServiceTests.cs`

If file does not exist, create the tests directory structure first.

- [ ] **Step 2: Write the failing test for `GenerateBindingsPreviewRoot`**

Create or extend `build/_build.Tests/Unit/Host/Paths/PathServiceTests.cs`:

```csharp
using Build.Host.Paths;
using Build.Tests.Fixtures;
using Cake.Core.IO;

namespace Build.Tests.Unit.Host.Paths;

public sealed class PathServiceTests
{
    [Test]
    public async Task GenerateBindingsPreviewRoot_Should_Return_Artifacts_Subdirectory()
    {
        var world = FakeCakeWorld.CreateLinux();
        var service = new PathService(world.RepoRoot, world.CreateParsedArguments(), world.Log);

        var preview = service.GenerateBindingsPreviewRoot;

        await Assert.That(preview.FullPath).EndsWith("artifacts/generated-bindings-preview");
    }

    [Test]
    public async Task GetGenerateBindingsPreviewFamilyRoot_Should_Combine_Family_Slug()
    {
        var world = FakeCakeWorld.CreateLinux();
        var service = new PathService(world.RepoRoot, world.CreateParsedArguments(), world.Log);

        var familyRoot = service.GetGenerateBindingsPreviewFamilyRoot("sdl2-core");

        await Assert.That(familyRoot.FullPath).EndsWith("artifacts/generated-bindings-preview/sdl2-core");
    }

    [Test]
    public void GetGenerateBindingsPreviewFamilyRoot_Should_Throw_When_Family_Is_Whitespace()
    {
        var world = FakeCakeWorld.CreateLinux();
        var service = new PathService(world.RepoRoot, world.CreateParsedArguments(), world.Log);

        Assert.Throws<ArgumentException>(() => service.GetGenerateBindingsPreviewFamilyRoot("   "));
    }
}
```

Note: `FakeCakeWorld.CreateParsedArguments()` may not exist yet. If not, use the public `PathService` constructor signature directly — read `PathService.cs:174` to see what `ParsedArguments` it expects, then construct one inline. If `FakeCakeWorld` exposes `Log` and `RepoRoot` (it does), and there's a helper for `ParsedArguments`, prefer that helper. Otherwise inline:

```csharp
var parsedArgs = new ParsedArguments(
    RepoRoot: null, Config: "Release", VcpkgDir: null, VcpkgInstalledDir: null,
    Library: Array.Empty<string>(), Rid: "linux-x64", Dll: Array.Empty<string>(),
    Suffix: null, Scope: Array.Empty<string>(), ExplicitVersion: Array.Empty<string>(),
    ExplicitVersions: null, VersionsFile: null);
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PathServiceTests"`

Expected: FAIL — `IPathService` does not define `GenerateBindingsPreviewRoot` or `GetGenerateBindingsPreviewFamilyRoot`.

- [ ] **Step 4: Add the interface members + implementation**

Modify `build/_build/Host/Paths/PathService.cs`. In the `IPathService` interface, after the existing `InspectOutputRoot` / `GetInspectOutputRidDir` / `GetInspectOutputLibraryDir` properties (around line 67-77), insert:

```csharp
/// <summary>
/// artifacts/generated-bindings-preview/ — Stage 1 scratch loop output root.
/// Temporary scaffolding; retires when Task 5/7 of Stage 1 lands real emitters
/// and production-location flag-flip to src/SDL2.{Family}/Generated/. Do not consume
/// outside build/_build/Targets/GenerateBindings/ or tools.cs generate-bindings.
/// </summary>
DirectoryPath GenerateBindingsPreviewRoot { get; }

/// <summary>
/// artifacts/generated-bindings-preview/{family}/ — per-family scratch loop output.
/// See <see cref="GenerateBindingsPreviewRoot"/> for retirement criteria.
/// </summary>
DirectoryPath GetGenerateBindingsPreviewFamilyRoot(string family);
```

In the `PathService` implementation, after `GetInspectOutputLibraryDir` (around line 350), add:

```csharp
public DirectoryPath GenerateBindingsPreviewRoot
    => ArtifactsDir.Combine("generated-bindings-preview");

public DirectoryPath GetGenerateBindingsPreviewFamilyRoot(string family)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(family);
    return GenerateBindingsPreviewRoot.Combine(family);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PathServiceTests"`

Expected: PASS for all three tests added in Step 2.

- [ ] **Step 6: Commit (await approval)**

Present to Deniz for approval:
```
feat(build): add temporary GenerateBindingsPreviewRoot path helpers (Stage 1 scratch loop)

IPathService gains GenerateBindingsPreviewRoot + GetGenerateBindingsPreviewFamilyRoot
to root the binding-autogen Stage 1 scratch output under artifacts/generated-bindings-preview/.
Retirement criteria documented in interface XML comments; replaced by per-family API when
Stage 1 Task 5/7 lands real emitters + production-location flag-flip.

Refs: docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md §4.2
```

After approval:
```pwsh
git add build/_build/Host/Paths/PathService.cs build/_build.Tests/Unit/Host/Paths/PathServiceTests.cs
git commit -m "..."
```

---

## Task 2: Create Pure Model Records

**Files:**
- Create: `build/_build/Targets/GenerateBindings/Model/PreviewBindingModel.cs`
- Create: `build/_build/Targets/GenerateBindings/Model/PreviewParseView.cs`
- Create: `build/_build/Targets/GenerateBindings/Model/PreviewFunction.cs`
- Create: `build/_build/Targets/GenerateBindings/Model/PreviewParameter.cs`

Pure records — no tests (records are trivially correct; emitter tests in Task 4 exercise them).

- [ ] **Step 1: Create PreviewParameter.cs**

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed record PreviewParameter(string Type, string Name);
```

- [ ] **Step 2: Create PreviewFunction.cs**

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed record PreviewFunction(
    string Name,
    string ReturnType,
    IReadOnlyList<PreviewParameter> Parameters,
    string SourceHeader);
```

- [ ] **Step 3: Create PreviewParseView.cs**

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed record PreviewParseView(
    string Name,
    string? SupportedOsPlatform,
    IReadOnlyList<PreviewFunction> Functions);
```

- [ ] **Step 4: Create PreviewBindingModel.cs**

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed record PreviewBindingModel(IReadOnlyList<PreviewParseView> Views);
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build build/_build/Build.csproj -c Release`

Expected: PASS. No new tests yet (Task 4 will add emitter tests that exercise these records).

- [ ] **Step 6: Commit (await approval)**

Proposed message:
```
feat(build): add Stage 1 preview binding model records

PreviewBindingModel + PreviewParseView + PreviewFunction + PreviewParameter
form the pure (Cake-free) translation target for CppAst parse output and the
input to the preview emitter. Records are seeds of the Task 4 real binding
model — rename via git mv when Task 4 lands.

Refs: spec §4.1 Model/
```

---

## Task 3: Create CppAst → Preview Model Translator

**Files:**
- Create: `build/_build/Targets/GenerateBindings/Model/CppAstToPreviewModel.cs`

PURE static class. Ports the spike's `MapType` / `MapPrimitive` / `MapPointer` logic (`tools/binding-spike/cppast-platform/generator/Program.cs:345-424`) into a reusable translator. Takes `IReadOnlyList<CppAstParseResult>` (existing record at `build/_build/Targets/GenerateBindings/Parsing/CppAstParseResult.cs`), returns `PreviewBindingModel`.

No unit tests — translator semantics validated by manual smoke per spec §7.1.

- [ ] **Step 1: Create CppAstToPreviewModel.cs**

```csharp
using Build.Targets.GenerateBindings.Parsing;
using CppAst;

namespace Build.Targets.GenerateBindings.Model;

internal static class CppAstToPreviewModel
{
    public static PreviewBindingModel Translate(IReadOnlyList<CppAstParseResult> parseResults)
    {
        ArgumentNullException.ThrowIfNull(parseResults);

        var neutralFunctions = ExtractNeutralFunctionNames(parseResults);
        var views = new List<PreviewParseView>(parseResults.Count);

        foreach (var result in parseResults)
        {
            var isNeutral = string.Equals(result.ParseView.Name, "Neutral", StringComparison.Ordinal);
            var functions = ExtractFunctions(result.Compilation);

            // Platform views drop functions that already appeared in the Neutral view.
            // Mirrors the spike's ExcludeNeutralSymbols rule
            // (tools/binding-spike/cppast-platform/generator/Program.cs:182-189).
            if (!isNeutral)
            {
                functions = functions
                    .Where(f => !neutralFunctions.Contains(f.Name))
                    .ToList();
            }

            views.Add(new PreviewParseView(
                Name: result.ParseView.Name,
                SupportedOsPlatform: result.ParseView.SupportedOsPlatform,
                Functions: functions));
        }

        return new PreviewBindingModel(views);
    }

    private static HashSet<string> ExtractNeutralFunctionNames(IReadOnlyList<CppAstParseResult> parseResults)
    {
        var neutral = parseResults.FirstOrDefault(r => string.Equals(r.ParseView.Name, "Neutral", StringComparison.Ordinal));
        if (neutral is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return ExtractFunctions(neutral.Compilation)
            .Select(f => f.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static List<PreviewFunction> ExtractFunctions(CppCompilation compilation)
    {
        return compilation.Functions
            .Where(f => IsSdl2Header(f.SourceFile) && !string.IsNullOrWhiteSpace(f.Name))
            .Select(ToFunction)
            .OrderBy(f => f.SourceHeader, StringComparer.Ordinal)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsSdl2Header(string? sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile))
        {
            return false;
        }

        var normalized = sourceFile.Replace('\\', '/');
        return normalized.Contains("/SDL2/", StringComparison.OrdinalIgnoreCase);
    }

    private static PreviewFunction ToFunction(CppFunction function)
    {
        var sourceHeader = Path.GetFileName(function.SourceFile ?? string.Empty);
        var parameters = function.Parameters
            .Select(p => new PreviewParameter(MapType(p.Type), SafeIdentifier(p.Name)))
            .ToList();

        return new PreviewFunction(
            Name: function.Name,
            ReturnType: MapType(function.ReturnType),
            Parameters: parameters,
            SourceHeader: sourceHeader);
    }

    private static string SafeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "@_";
        }
        return name switch
        {
            "ref" or "out" or "in" or "params" or "object" or "string" or "event" => "@" + name,
            _ => name,
        };
    }

    private static string MapType(CppType type)
    {
        while (type is CppQualifiedType qt)
        {
            type = qt.ElementType;
        }

        return type switch
        {
            CppPrimitiveType prim => MapPrimitive(prim),
            CppPointerType ptr => MapPointer(ptr),
            CppTypedef td => MapTypedef(td),
            CppArrayType arr => MapPointer(new CppPointerType(arr.ElementType)),
            CppEnum => "int",
            CppClass cls => cls.Name,
            _ => "IntPtr",
        };
    }

    private static string MapPrimitive(CppPrimitiveType prim) => prim.Kind switch
    {
        CppPrimitiveKind.Void => "void",
        CppPrimitiveKind.Bool => "byte",
        CppPrimitiveKind.Char => "sbyte",
        CppPrimitiveKind.WChar => "char",
        CppPrimitiveKind.Short => "short",
        CppPrimitiveKind.Int => "int",
        CppPrimitiveKind.LongLong => "long",
        CppPrimitiveKind.UnsignedChar => "byte",
        CppPrimitiveKind.UnsignedShort => "ushort",
        CppPrimitiveKind.UnsignedInt => "uint",
        CppPrimitiveKind.UnsignedLongLong => "ulong",
        CppPrimitiveKind.Float => "float",
        CppPrimitiveKind.Double => "double",
        CppPrimitiveKind.Long => "int",
        CppPrimitiveKind.UnsignedLong => "uint",
        _ => "IntPtr",
    };

    private static string MapPointer(CppPointerType ptr)
    {
        var element = ptr.ElementType;
        while (element is CppQualifiedType qt)
        {
            element = qt.ElementType;
        }

        return element switch
        {
            CppPrimitiveType { Kind: CppPrimitiveKind.Void } => "IntPtr",
            CppPrimitiveType prim => MapPrimitive(prim) + "*",
            CppTypedef td when td.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
            CppTypedef td => MapTypedefPointer(td),
            CppClass cls when cls.Name.StartsWith("ID", StringComparison.Ordinal) => "IntPtr",
            CppClass cls when cls.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
            CppClass cls => cls.Name + "*",
            _ => "IntPtr",
        };
    }

    private static string MapTypedefPointer(CppTypedef td)
    {
        var element = td.ElementType;
        while (element is CppQualifiedType qt)
        {
            element = qt.ElementType;
        }

        if (element is CppPrimitiveType prim)
        {
            return MapPrimitive(prim) + "*";
        }
        if (element is CppTypedef nested)
        {
            return MapTypedefPointer(nested);
        }
        return td.Name + "*";
    }

    private static string MapTypedef(CppTypedef td) => td.Name switch
    {
        "Sint8" => "sbyte",
        "Uint8" => "byte",
        "Sint16" => "short",
        "Uint16" => "ushort",
        "Sint32" => "int",
        "Uint32" => "uint",
        "Sint64" => "long",
        "Uint64" => "ulong",
        "SDL_bool" => "byte",
        "size_t" => "nuint",
        "ptrdiff_t" => "nint",
        _ when td.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
        _ => MapType(td.ElementType),
    };
}
```

- [ ] **Step 2: Build to verify compiles**

Run: `dotnet build build/_build/Build.csproj -c Release`

Expected: PASS.

- [ ] **Step 3: Commit (await approval)**

Proposed message:
```
feat(build): add CppAst → PreviewBindingModel translator (Stage 1 scratch)

Pure translator ported from the binding-spike platform-pass generator
(tools/binding-spike/cppast-platform/generator/Program.cs:182-424).
Drops functions seen in Neutral from each platform view (deduplication
mirrors the spike's ExcludeNeutralSymbols rule). Type mapping covers
primitive, pointer, typedef, enum, class, qualified, and array shapes
with SDL_-prefixed opaque pointers folded to IntPtr.

No unit tests: Stage 1 placeholder validated by manual smoke per spec §7.1;
real translator + dedicated tests land with Task 4 binding model.

Refs: spec §4.1 Model/, §7.1
```

---

## Task 4: Create Emitter, GeneratedFileSet, and Tests

**Files:**
- Create: `build/_build/Targets/GenerateBindings/Emitting/GeneratedFile.cs`
- Create: `build/_build/Targets/GenerateBindings/Emitting/GeneratedFileSet.cs`
- Create: `build/_build/Targets/GenerateBindings/Emitting/PreviewParseViewReport.cs`
- Create: `build/_build/Targets/GenerateBindings/Emitting/PreviewEmitter.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/PreviewBindingModelData.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/PreviewEmitterTests.cs`
- Create: `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/PreviewParseViewReportTests.cs`

PURE emitter; takes `PreviewBindingModel`, returns `GeneratedFileSet` containing 8 `.g.cs` files + 1 `parse-views.json` (serialized via `System.Text.Json`, BCL not Cake — pure stays pure). Deterministic file ordering, deterministic function ordering within each file.

- [ ] **Step 1: Create GeneratedFile.cs**

```csharp
namespace Build.Targets.GenerateBindings.Emitting;

internal sealed record GeneratedFile(string RelativePath, string Content);
```

- [ ] **Step 2: Create GeneratedFileSet.cs**

```csharp
namespace Build.Targets.GenerateBindings.Emitting;

internal sealed record GeneratedFileSet(IReadOnlyList<GeneratedFile> Files);
```

- [ ] **Step 3: Create PreviewParseViewReport.cs**

```csharp
namespace Build.Targets.GenerateBindings.Emitting;

internal sealed record PreviewParseViewReport(IReadOnlyList<PreviewParseViewReportEntry> Views);

internal sealed record PreviewParseViewReportEntry(
    string Name,
    string? SupportedOSPlatform,
    int FunctionCount,
    IReadOnlyList<PreviewParseViewReportFunction> Functions);

internal sealed record PreviewParseViewReportFunction(
    string Name,
    string SourceHeader);
```

- [ ] **Step 4: Create PreviewBindingModelData.cs (centralized inline test data)**

```csharp
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
                // Same as neutral — should be deduped to nothing in emitter output if
                // the translator already deduped, OR emitted with annotation if not.
                // For these tests we assume the model is already deduped (translator job).
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
```

- [ ] **Step 5: Write the failing PreviewEmitter tests**

Create `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/PreviewEmitterTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Emitting;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emitting;

public sealed class PreviewEmitterTests
{
    [Test]
    public async Task Emit_Should_Produce_One_File_Per_Parse_View_Plus_Report()
    {
        var model = PreviewBindingModelData.TwoViewsNeutralPlusLinux();
        var emitter = new PreviewEmitter();

        var fileSet = emitter.Emit(model);

        var relativePaths = fileSet.Files.Select(f => f.RelativePath).ToArray();
        await Assert.That(relativePaths).IsEquivalentTo(
        [
            "Platform/Neutral/Commands.g.cs",
            "Platform/Linux/Commands.g.cs",
            "parse-views.json",
        ]);
    }

    [Test]
    public async Task Emit_Should_Produce_DllImport_Stub_Per_Function()
    {
        var model = PreviewBindingModelData.TwoViewsNeutralPlusLinux();
        var emitter = new PreviewEmitter();

        var fileSet = emitter.Emit(model);

        var neutralFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(neutralFile.Content).Contains("[DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]");
        await Assert.That(neutralFile.Content).Contains("internal static extern int SDL_Init(uint flags);");
        await Assert.That(neutralFile.Content).Contains("internal static extern void SDL_Quit();");
    }

    [Test]
    public async Task Emit_Should_Annotate_Platform_Views_With_SupportedOSPlatform()
    {
        var model = PreviewBindingModelData.TwoViewsNeutralPlusLinux();
        var emitter = new PreviewEmitter();

        var fileSet = emitter.Emit(model);

        var linuxFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Linux/Commands.g.cs");
        await Assert.That(linuxFile.Content).Contains("[SupportedOSPlatform(\"linux\")]");
    }

    [Test]
    public async Task Emit_Should_Not_Annotate_Neutral_View_With_SupportedOSPlatform()
    {
        var model = PreviewBindingModelData.TwoViewsNeutralPlusLinux();
        var emitter = new PreviewEmitter();

        var fileSet = emitter.Emit(model);

        var neutralFile = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(neutralFile.Content).DoesNotContain("SupportedOSPlatform");
    }

    [Test]
    public async Task Emit_Should_Include_LibName_Constant_In_Each_View_File()
    {
        var model = PreviewBindingModelData.TwoViewsNeutralPlusLinux();
        var emitter = new PreviewEmitter();

        var fileSet = emitter.Emit(model);

        foreach (var file in fileSet.Files.Where(f => f.RelativePath.EndsWith(".g.cs", StringComparison.Ordinal)))
        {
            await Assert.That(file.Content).Contains("private const string LibName = \"SDL2\";");
        }
    }

    [Test]
    public async Task Emit_Should_Be_Deterministic_Across_Invocations()
    {
        var model = PreviewBindingModelData.TwoViewsNeutralPlusLinux();
        var emitter = new PreviewEmitter();

        var first = emitter.Emit(model);
        var second = emitter.Emit(model);

        for (var i = 0; i < first.Files.Count; i++)
        {
            await Assert.That(second.Files[i].RelativePath).IsEqualTo(first.Files[i].RelativePath);
            await Assert.That(second.Files[i].Content).IsEqualTo(first.Files[i].Content);
        }
    }

    [Test]
    public async Task Emit_Should_Handle_Function_With_No_Parameters()
    {
        var model = PreviewBindingModelData.SingleNeutralEmptyParameterFunction();
        var emitter = new PreviewEmitter();

        var fileSet = emitter.Emit(model);

        var file = fileSet.Files.Single(f => f.RelativePath == "Platform/Neutral/Commands.g.cs");
        await Assert.That(file.Content).Contains("internal static extern uint SDL_GetTicks();");
    }

    [Test]
    public async Task Emit_Should_Produce_Empty_Files_Collection_For_Empty_Model()
    {
        var model = PreviewBindingModelData.EmptyModel();
        var emitter = new PreviewEmitter();

        var fileSet = emitter.Emit(model);

        // Empty model still emits the parse-views.json (an empty array); no .g.cs files.
        await Assert.That(fileSet.Files).HasCount(1);
        await Assert.That(fileSet.Files[0].RelativePath).IsEqualTo("parse-views.json");
    }
}
```

- [ ] **Step 6: Write the failing PreviewParseViewReport tests**

Create `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/PreviewParseViewReportTests.cs`:

```csharp
using System.Text.Json;
using Build.Targets.GenerateBindings.Emitting;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emitting;

public sealed class PreviewParseViewReportTests
{
    [Test]
    public async Task PreviewParseViewReport_Should_Round_Trip_Through_System_Text_Json()
    {
        var original = new PreviewParseViewReport(
        [
            new PreviewParseViewReportEntry(
                Name: "Neutral",
                SupportedOSPlatform: null,
                FunctionCount: 2,
                Functions:
                [
                    new PreviewParseViewReportFunction("SDL_Init", "SDL_main.h"),
                    new PreviewParseViewReportFunction("SDL_Quit", "SDL_main.h"),
                ]),
            new PreviewParseViewReportEntry(
                Name: "Linux",
                SupportedOSPlatform: "linux",
                FunctionCount: 1,
                Functions:
                [
                    new PreviewParseViewReportFunction("SDL_LinuxSetThreadPriority", "SDL_system.h"),
                ]),
        ]);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(original, options);
        var round = JsonSerializer.Deserialize<PreviewParseViewReport>(json, options);

        await Assert.That(round).IsNotNull();
        await Assert.That(round!.Views).HasCount(2);
        await Assert.That(round.Views[0].Name).IsEqualTo("Neutral");
        await Assert.That(round.Views[0].Functions).HasCount(2);
        await Assert.That(round.Views[1].SupportedOSPlatform).IsEqualTo("linux");
    }
}
```

- [ ] **Step 7: Run tests to verify they fail**

Run: `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PreviewEmitterTests|FullyQualifiedName~PreviewParseViewReportTests"`

Expected: FAIL — `PreviewEmitter` class does not exist yet. The `PreviewParseViewReport` round-trip test may pass already since records auto-serialize.

- [ ] **Step 8: Implement PreviewEmitter**

Create `build/_build/Targets/GenerateBindings/Emitting/PreviewEmitter.cs`:

```csharp
using System.Text;
using System.Text.Json;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emitting;

internal sealed class PreviewEmitter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public GeneratedFileSet Emit(PreviewBindingModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var files = new List<GeneratedFile>();

        foreach (var view in model.Views)
        {
            files.Add(new GeneratedFile(
                RelativePath: $"Platform/{view.Name}/Commands.g.cs",
                Content: EmitCommandsFile(view)));
        }

        files.Add(new GeneratedFile(
            RelativePath: "parse-views.json",
            Content: EmitReportJson(model)));

        return new GeneratedFileSet(files);
    }

    private static string EmitCommandsFile(PreviewParseView view)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("// Stage 1 binding-autogen preview output. Placeholder shape;");
        builder.AppendLine("// replaced by real emitter at Stage 1 Task 5.");
        builder.Append("// Parse view: ").AppendLine(view.Name);
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Runtime.InteropServices;");
        if (view.SupportedOsPlatform is not null)
        {
            builder.AppendLine("using System.Runtime.Versioning;");
        }

        builder.AppendLine();
        builder.AppendLine("namespace Janset.Sdl2.Preview;");
        builder.AppendLine();
        builder.Append("internal static unsafe partial class Sdl2Preview_").AppendLine(view.Name);
        builder.AppendLine("{");
        builder.AppendLine("    private const string LibName = \"SDL2\";");

        foreach (var function in view.Functions)
        {
            builder.AppendLine();
            if (view.SupportedOsPlatform is not null)
            {
                builder.Append("    [SupportedOSPlatform(\"").Append(view.SupportedOsPlatform).AppendLine("\")]");
            }

            builder.AppendLine("    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]");
            builder.AppendLine("    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]");
            builder
                .Append("    internal static extern ")
                .Append(function.ReturnType)
                .Append(' ')
                .Append(function.Name)
                .Append('(')
                .Append(JoinParameters(function.Parameters))
                .AppendLine(");");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string JoinParameters(IReadOnlyList<PreviewParameter> parameters)
    {
        return string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"));
    }

    private static string EmitReportJson(PreviewBindingModel model)
    {
        var entries = model.Views.Select(view =>
            new PreviewParseViewReportEntry(
                Name: view.Name,
                SupportedOSPlatform: view.SupportedOsPlatform,
                FunctionCount: view.Functions.Count,
                Functions: view.Functions
                    .Select(f => new PreviewParseViewReportFunction(f.Name, f.SourceHeader))
                    .ToList()))
            .ToList();

        var report = new PreviewParseViewReport(entries);
        return JsonSerializer.Serialize(report, JsonOptions);
    }
}
```

- [ ] **Step 9: Add `System.Runtime.Versioning` using line check**

The emitter generates `using System.Runtime.Versioning;` lines in non-Neutral .g.cs files. The host project where Stage 1 scratch output lands (`artifacts/generated-bindings-preview/`) is not compiled — these strings live as text in the scratch directory. No project reference change needed.

- [ ] **Step 10: Run tests to verify they pass**

Run: `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~PreviewEmitterTests|FullyQualifiedName~PreviewParseViewReportTests"`

Expected: All 9 tests PASS.

- [ ] **Step 11: Commit (await approval)**

Proposed message:
```
feat(build): add Stage 1 preview emitter + parse-view JSON sidecar

PreviewEmitter (PURE, no Cake) takes PreviewBindingModel and produces a
GeneratedFileSet containing one Platform/<View>/Commands.g.cs per parse
view (bare DllImport stubs in the spike's shape) plus parse-views.json
report. Emitter uses System.Text.Json from BCL — does not violate the
Cake-nativeness rule (purely a data serialization library, not IO).

9 unit tests cover file-set shape, DllImport stub content, OS-platform
annotation discipline, LibName constant presence, deterministic ordering,
no-parameter functions, empty-model handling, and report JSON round-trip.

Refs: spec §4.1 Emitting/, §7.2
```

---

## Task 5: Create IBindingGenerationRunner + BindingGenerationRunner (Cake-aware)

**Files:**
- Create: `build/_build/Targets/GenerateBindings/BindingGenerationRunner.cs` (interface + sealed impl, single file)

Cake-aware sealed orchestrator. Wires `HeaderSetResolver` → `CppAstParseRunner` → `CppAstToPreviewModel` → `PreviewEmitter`, asserts neutral view non-empty, asserts libclang resolved version matches CppAst-expected trio, writes `GeneratedFileSet` through Cake-native IO.

Interface is justified per ADR-002 §2.5: scenario tests inject a stub `IBindingGenerationRunner` to assert Cake target orchestration without running the real parser.

- [ ] **Step 1: Create BindingGenerationRunner.cs with interface + impl**

```csharp
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using ClangSharp.Interop;

namespace Build.Targets.GenerateBindings;

internal interface IBindingGenerationRunner
{
    Task RunAsync(GenerateBindingsRequest request, CancellationToken cancellationToken);
}

internal sealed class BindingGenerationRunner(
    ICakeContext cakeContext,
    HeaderSetResolver headerResolver,
    CppAstParseRunner parseRunner,
    PreviewEmitter emitter,
    ICakeLog log) : IBindingGenerationRunner
{
    // ClangSharp libclang transitively pulled by CppAst expects 20.1.x.
    // See docs/binding-autogen/research/binding-autogen-spike-findings.md §7.2 friction #5
    // for the libclang 21.x stack-overflow incident that motivated this assertion.
    private const string ExpectedLibclangMajorMinor = "20.1";

    private readonly ICakeContext _context = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly HeaderSetResolver _headerResolver = headerResolver ?? throw new ArgumentNullException(nameof(headerResolver));
    private readonly CppAstParseRunner _parseRunner = parseRunner ?? throw new ArgumentNullException(nameof(parseRunner));
    private readonly PreviewEmitter _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public async Task RunAsync(GenerateBindingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        AssertLibclangVersion();
        LogContainerDigestIfPresent();

        var headerSet = _headerResolver.ResolveSdl2CoreHeaders(request.VcpkgInstalledDirectory, request.Triplet);
        _log.Information("Resolved {0} SDL2 headers under '{1}'.", headerSet.Headers.Count, headerSet.Sdl2IncludeDirectory.FullPath);

        var catalog = PlatformCatalog.CreateSdl2Catalog();
        var parseResults = new List<CppAstParseResult>(catalog.ParseViews.Count);
        foreach (var view in catalog.ParseViews)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _log.Information("Parsing view '{0}' ({1} defines, {2} undefines).",
                view.Name, view.Defines.Count, view.Undefines.Count);
            parseResults.Add(_parseRunner.Parse(headerSet, view));
        }

        var model = CppAstToPreviewModel.Translate(parseResults);
        EnsureNeutralViewNonEmpty(model);
        LogPerViewCounts(model);

        var fileSet = _emitter.Emit(model);
        await WriteAsync(fileSet, request.OutputDirectory, cancellationToken).ConfigureAwait(false);

        _log.Information("Wrote {0} files to '{1}'.", fileSet.Files.Count, request.OutputDirectory.FullPath);
    }

    private void AssertLibclangVersion()
    {
        var resolved = ResolveLibclangVersion();
        _log.Information("libclang resolved version: {0}", resolved);
        if (!resolved.Contains(ExpectedLibclangMajorMinor, StringComparison.Ordinal))
        {
            throw new CakeException(
                $"libclang version mismatch: expected {ExpectedLibclangMajorMinor}.x (CppAst 0.24.0 trio), " +
                $"resolved '{resolved}'. Check Directory.Packages.props + packages.lock.json. " +
                "See docs/playbook/binding-generator-maintenance.md §Trio Pinning.");
        }
    }

    private static string ResolveLibclangVersion()
    {
        // ClangSharp.Interop.clang.getClangVersion() returns a CXString from libclang.
        // The pattern below mirrors how ClangSharp samples extract the version string.
        var cxString = clang.getClangVersion();
        try
        {
            return cxString.ToString() ?? string.Empty;
        }
        finally
        {
            cxString.Dispose();
        }
    }

    private void LogContainerDigestIfPresent()
    {
        var digest = Environment.GetEnvironmentVariable("CONTAINER_DIGEST");
        if (!string.IsNullOrWhiteSpace(digest))
        {
            _log.Information("linux-builder container digest: {0}", digest);
        }
    }

    private static void EnsureNeutralViewNonEmpty(PreviewBindingModel model)
    {
        var neutral = model.Views.FirstOrDefault(v => string.Equals(v.Name, "Neutral", StringComparison.Ordinal));
        if (neutral is null || neutral.Functions.Count == 0)
        {
            throw new CakeException(
                "Neutral parse view returned 0 SDL2 functions. Header set or platform macro hygiene likely misconfigured. " +
                "Inspect PlatformCatalog.AllPlatformMacros and verify the SDL2 header tree under vcpkg_installed.");
        }
    }

    private void LogPerViewCounts(PreviewBindingModel model)
    {
        foreach (var view in model.Views)
        {
            _log.Information("View {0}: {1} functions.", view.Name, view.Functions.Count);
        }
    }

    private async Task WriteAsync(GeneratedFileSet fileSet, DirectoryPath outputDirectory, CancellationToken cancellationToken)
    {
        _context.EnsureDirectoryExists(outputDirectory);

        foreach (var file in fileSet.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = outputDirectory.CombineWithFilePath(file.RelativePath);
            var parent = targetPath.GetDirectory();
            _context.EnsureDirectoryExists(parent);

            await File.WriteAllTextAsync(targetPath.FullPath, file.Content, cancellationToken).ConfigureAwait(false);
        }
    }
}
```

**Note on `File.WriteAllTextAsync`:** Cake's built-in `WriteAllTextAsync` extension exists on `ICakeContext` for some Cake versions — verify against this repo's Cake version. If `context.WriteAllTextAsync(targetPath, content)` is available (used in `GeneratedStampRepository.cs:37`), prefer it over `System.IO.File.WriteAllTextAsync`. Inspect `build/_build/Host/Cake/CakeFileSystemExtensions.cs` (or similar) before settling.

- [ ] **Step 2: Verify Cake context async-write extension surface**

Run: `Grep -r "WriteAllTextAsync" build/_build/Host/Cake/`

If a Cake-native async-write extension exists, switch the `WriteAsync` body to use it. Example:

```csharp
await _context.WriteAllTextAsync(targetPath, file.Content).ConfigureAwait(false);
```

If only the sync `FileWriteText` Cake alias exists, the `File.WriteAllTextAsync` fallback above stays — it is one of the rare BCL-IO exceptions justified by needing async + CancellationToken support at a build-host write boundary that Cake doesn't model. Add a one-line comment noting the rationale.

- [ ] **Step 3: Build to verify compiles**

Run: `dotnet build build/_build/Build.csproj -c Release`

Expected: PASS.

- [ ] **Step 4: Commit (await approval)**

Proposed message:
```
feat(build): add BindingGenerationRunner (Cake-aware orchestrator + interface)

BindingGenerationRunner wires HeaderSetResolver → CppAstParseRunner →
CppAstToPreviewModel → PreviewEmitter and writes the GeneratedFileSet
through Cake-native IO. Three fail-closed guards: libclang version
assertion (matches CppAst 0.24.0 trio per ADR-004), neutral-view
non-empty check, and per-step CancellationToken honoring. Logs
resolved libclang version + linux-builder container digest (when
CONTAINER_DIGEST env var present) for audit.

IBindingGenerationRunner interface earned per ADR-002 §2.5: scenario
tests inject a stub for Cake target orchestration assertions.

Refs: spec §4.1, §5.2, §6.2, §8.2
```

---

## Task 6: Create GenerateBindingsTask

**Files:**
- Create: `build/_build/Targets/GenerateBindings/GenerateBindingsTask.cs`
- Modify: `build/_build/Targets/GenerateBindings/GenerateBindingsRequest.cs` (verify or reshape; see Step 1)

Cake task with `[TaskName("GenerateBindings")]`, depends on `EnsureVcpkgDependencies`. Reads `BuildContext`, asserts triplet starts with `linux-`, builds `GenerateBindingsRequest`, calls runner, translates failures.

- [ ] **Step 1: Verify GenerateBindingsRequest shape**

Read `build/_build/Targets/GenerateBindings/GenerateBindingsRequest.cs`. Current shape (from Task 1 of Stage 1):

```csharp
internal sealed record GenerateBindingsRequest(
    Sdl2CoreGenerationConfig Config,
    DirectoryPath VcpkgInstalledDirectory,
    string Triplet,
    DirectoryPath OutputDirectory);
```

This shape is correct for this slice — no modification needed.

- [ ] **Step 2: Create GenerateBindingsTask.cs**

```csharp
using Build.Host;
using Build.Targets.EnsureVcpkgDependencies;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Targets.GenerateBindings;

[TaskName("GenerateBindings")]
[TaskDescription("Generates Stage 1 SDL2.Core preview bindings (Linux-canonical, runs inside linux-builder container).")]
[IsDependentOn(typeof(EnsureVcpkgDependenciesTask))]
public sealed class GenerateBindingsTask(
    IBindingGenerationRunner runner,
    ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly IBindingGenerationRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var triplet = context.Runtime.Triplet;
        AssertLinuxTriplet(context.RuntimeIdentifier, triplet);

        var vcpkgInstalledTripletDir = context.Paths.GetVcpkgInstalledTripletDir(triplet);
        var outputDirectory = context.Paths.GetGenerateBindingsPreviewFamilyRoot("sdl2-core");

        _log.Information("Generating SDL2.Core preview bindings to '{0}' (triplet '{1}').",
            outputDirectory.FullPath, triplet);

        var request = new GenerateBindingsRequest(
            Config: Sdl2CoreGenerationConfig.Create(),
            VcpkgInstalledDirectory: vcpkgInstalledTripletDir.GetParent()!,
            Triplet: triplet,
            OutputDirectory: outputDirectory);

        try
        {
            await _runner.RunAsync(request, context.CancellationToken).ConfigureAwait(false);
        }
        catch (CakeException)
        {
            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CakeException($"GenerateBindings failed: {ex.Message}", ex);
        }
    }

    private static void AssertLinuxTriplet(string rid, string triplet)
    {
        if (!triplet.StartsWith("linux-", StringComparison.Ordinal) &&
            !triplet.StartsWith("x64-linux", StringComparison.Ordinal) &&
            !triplet.StartsWith("arm64-linux", StringComparison.Ordinal))
        {
            throw new CakeException(
                $"GenerateBindings is Linux-canonical; expected linux-x64 host RID inside linux-builder container; " +
                $"got RID '{rid}' / triplet '{triplet}'. Invoke via 'tools.cs generate-bindings'.");
        }
    }
}
```

- [ ] **Step 3: Verify the `Sdl2CoreGenerationConfig.Create()` factory exists**

Read `build/_build/Targets/GenerateBindings/Sdl2CoreGenerationConfig.cs`. If the file exposes a static factory like `Create()` or `Default()`, use it. If it requires explicit constructor args (header list, namespace, class name), construct inline:

```csharp
Config: new Sdl2CoreGenerationConfig(...),
```

Adjust the `var request = ...` block in `GenerateBindingsTask.cs` accordingly. Build to confirm.

- [ ] **Step 4: Build to verify compiles**

Run: `dotnet build build/_build/Build.csproj -c Release`

Expected: PASS.

- [ ] **Step 5: Commit (await approval)**

Proposed message:
```
feat(build): add GenerateBindings Cake task (Stage 1 scratch loop)

GenerateBindingsTask is the Cake entry point for Stage 1 preview generation.
Depends on EnsureVcpkgDependencies (so vcpkg install runs first inside the
linux-builder container). Asserts Linux triplet at task entry; reads paths
from IPathService + IRuntimeProfile, builds GenerateBindingsRequest, calls
IBindingGenerationRunner. Failure translation to CakeException keeps the
Cake log output consistent with the rest of the build host.

Refs: spec §4.1, §5.2, §6.2
```

---

## Task 7: DI Registration

**Files:**
- Modify: `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs`
- Modify: `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` (or equivalent — locate the existing smoke test)

`AddGenerateBindings()` already registers `ParseDiagnosticFormatter`, `CppAstParseRunner`, `HeaderSetResolver`, `HeaderSetFingerprintCalculator`. Extend to register `PreviewEmitter` + `IBindingGenerationRunner` / `BindingGenerationRunner` + the task class is auto-discovered via `[TaskName]` so it stays out of DI per ADR-002.

- [ ] **Step 1: Locate the composition-root smoke test**

Run: `Glob "build/_build.Tests/**/ServiceCollectionExtensionsSmokeTests.cs"`

Read the file to understand its existing pattern.

- [ ] **Step 2: Write the failing smoke test addition**

In the smoke test file, add a test asserting `AddGenerateBindings()` resolves `IBindingGenerationRunner` and `PreviewEmitter`:

```csharp
[Test]
public async Task AddGenerateBindings_Should_Resolve_BindingGenerationRunner_And_PreviewEmitter()
{
    var provider = ServiceCollectionTestHost.AddFakeCakeWorld()
        .AddGenerateBindings()
        .BuildServiceProvider();

    var runner = provider.GetService<IBindingGenerationRunner>();
    var emitter = provider.GetService<PreviewEmitter>();

    await Assert.That(runner).IsNotNull();
    await Assert.That(emitter).IsNotNull();
}
```

Add `using Build.Targets.GenerateBindings;` and `using Build.Targets.GenerateBindings.Emitting;` to the file.

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~AddGenerateBindings_Should_Resolve_BindingGenerationRunner_And_PreviewEmitter"`

Expected: FAIL — `IBindingGenerationRunner` not registered.

- [ ] **Step 4: Extend AddGenerateBindings**

Modify `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs`:

```csharp
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.GenerateBindings;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGenerateBindings(this IServiceCollection services)
    {
        services.AddSingleton<ParseDiagnosticFormatter>();
        services.AddSingleton<CppAstParseRunner>();
        services.AddSingleton<HeaderSetResolver>();
        services.AddSingleton<HeaderSetFingerprintCalculator>();
        services.AddSingleton<PreviewEmitter>();
        services.AddSingleton<IBindingGenerationRunner, BindingGenerationRunner>();

        return services;
    }
}
```

- [ ] **Step 5: Verify Program.cs already calls AddGenerateBindings**

Confirm `build/_build/Program.cs:131` reads `.AddGenerateBindings()`. If absent, add it in the composition-root chain after `.AddOtoolAnalyzeTarget()` and before `.AddPreFlightCheck()`.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~AddGenerateBindings_Should_Resolve_BindingGenerationRunner_And_PreviewEmitter"`

Expected: PASS.

- [ ] **Step 7: Commit (await approval)**

Proposed message:
```
feat(build): wire PreviewEmitter + BindingGenerationRunner into DI

AddGenerateBindings() now registers PreviewEmitter and the
IBindingGenerationRunner / BindingGenerationRunner pair so the Cake
target resolves at host startup. GenerateBindingsTask is auto-discovered
by Cake.Frosting via [TaskName] per ADR-002 §2.5; not in DI explicitly.

Composition-root smoke test extended to assert resolution.

Refs: spec §4.1, §7.4
```

---

## Task 8: Scenario Tests (Fail-Closed Paths)

**Files:**
- Create: `build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskScenarioTests.cs`

Three scenario tests inject a stub `IBindingGenerationRunner` test-double. Per testing-guidelines: canonical `FakeCakeWorld` + `TargetTestHost<TTask>`. No real parsing.

- [ ] **Step 1: Create the test file with a stub runner inline**

```csharp
using Build.Targets.GenerateBindings;
using Build.Tests.Fixtures;
using Cake.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class GenerateBindingsTaskScenarioTests
{
    [Test]
    public async Task RunAsync_Should_Throw_CakeException_When_Triplet_Not_Linux()
    {
        var world = FakeCakeWorld.CreateWindows();   // win-x64 / x64-windows-hybrid
        var stub = new StubBindingGenerationRunner();

        var host = new TargetTestHost<GenerateBindingsTask>(world)
            .WithServices(services =>
            {
                services.AddGenerateBindings();
                services.AddSingleton<IBindingGenerationRunner>(stub);
                services.AddData();
            });

        var result = await host.RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception).IsNotNull();
        await Assert.That(result.Exception!.Message).Contains("Linux-canonical");
        await Assert.That(stub.WasCalled).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Throw_CakeException_When_Header_Directory_Missing()
    {
        var world = FakeCakeWorld.CreateLinux();   // linux-x64 / x64-linux-hybrid
        // No SDL2 headers seeded — the real HeaderSetResolver will throw via the runner.
        // We use the REAL runner here so the failure surfaces from HeaderSetResolver,
        // not the stub. AddGenerateBindings registers the real runner.

        var host = new TargetTestHost<GenerateBindingsTask>(world)
            .WithServices(services =>
            {
                services.AddGenerateBindings();
                services.AddData();
            });

        var result = await host.RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception).IsNotNull();
        await Assert.That(result.Exception!.Message)
            .Satisfies(msg => msg.Contains("SDL2 include directory") || msg.Contains("No SDL2 headers"));
    }

    [Test]
    public async Task RunAsync_Should_Throw_CakeException_When_Runner_Reports_Empty_Neutral_View()
    {
        var world = FakeCakeWorld.CreateLinux();
        var stub = new StubBindingGenerationRunner
        {
            ExceptionToThrow = new CakeException("Neutral parse view returned 0 SDL2 functions."),
        };

        var host = new TargetTestHost<GenerateBindingsTask>(world)
            .WithServices(services =>
            {
                services.AddGenerateBindings();
                services.AddSingleton<IBindingGenerationRunner>(stub);
                services.AddData();
            });

        var result = await host.RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception).IsNotNull();
        await Assert.That(result.Exception!.Message).Contains("Neutral parse view");
        await Assert.That(stub.WasCalled).IsTrue();
    }

    private sealed class StubBindingGenerationRunner : IBindingGenerationRunner
    {
        public bool WasCalled { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task RunAsync(GenerateBindingsRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Verify FakeCakeWorld.CreateLinux() exposes triplet `x64-linux-hybrid`**

Read `build/_build.Tests/Fixtures/FakeCakeWorld.cs` to confirm `CreateLinux()` configures `IRuntimeProfile.Triplet = "x64-linux-hybrid"`. If it does not, add a `WithTriplet("x64-linux-hybrid")` fluent call to the test setup. Adjust tests accordingly.

- [ ] **Step 3: Run the tests to verify they pass**

Run: `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~GenerateBindingsTaskScenarioTests"`

Expected: All 3 tests PASS.

- [ ] **Step 4: Commit (await approval)**

Proposed message:
```
test(build): add GenerateBindings scenario tests (fail-closed paths)

Three TargetTestHost-based scenario tests cover:
  - non-linux triplet → CakeException (stub runner asserted uncalled)
  - missing SDL2 header dir → CakeException via real HeaderSetResolver
  - runner-reported empty neutral view → CakeException propagated

Stub IBindingGenerationRunner test-double inline in test class. Real-parse
happy path is validated by the manual `tools.cs generate-bindings` smoke
per spec §7.1, not in CI tests (libclang + Linux sysroot dependencies
exceed FakeCakeWorld surface).

Refs: spec §7.3
```

---

## Task 9: Create binding-generator.Dockerfile + Extend .dockerignore

**Files:**
- Create: `docker/binding-generator.Dockerfile`
- Modify: `.dockerignore` (or create `docker/binding-generator.dockerignore` if Docker context is scoped)

Derives from `linux-builder` via `ARG BASE_IMAGE`. Multi-layer: SDK install → NuGet restore → source COPY.

- [ ] **Step 1: Inspect existing .dockerignore**

Read `docker/.dockerignore` (or repo-root `.dockerignore` if it exists). The linux-builder workflow at `.github/workflows/build-linux-container.yml:78` uses `context: ./docker`, but our binding-generator needs the full repo as context. We will use a root-level `.dockerignore` that applies to binding-generator's build context (`docker build -f docker/binding-generator.Dockerfile .` from repo root).

- [ ] **Step 2: Create docker/binding-generator.Dockerfile**

```dockerfile
# syntax=docker/dockerfile:1.7
#
# Janset SDL2 bindings — Stage 1 binding generator image.
#
# Derived from linux-builder:focal-latest (sourced via ARG BASE_IMAGE from
# build/manifest.json runtimes[linux-x64].container_image). Adds .NET SDK
# 10.x (pinned via global.json), restores NuGet packages, and COPYs the
# repo source as the final layer so dev iteration only rebuilds the source
# layer on each code edit.
#
# Build context: repo root. Invoke from tools.cs generate-bindings —
# do not run `docker build` manually unless debugging the Dockerfile itself.
#
# Output artifact lands via bind-mount at run time (not baked into image).
# vcpkg binary cache lives in a named Docker volume (`janset-vcpkg-cache`).
#
# Slice ref: docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md §4.3

ARG BASE_IMAGE
FROM ${BASE_IMAGE}

# Layer A — .NET SDK 10 via dotnet-install.sh, pinned to global.json.
# Rebuilds only when global.json changes (SDK major/minor bump).
COPY global.json /tmp/global.json
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && chmod +x /tmp/dotnet-install.sh \
 && /tmp/dotnet-install.sh --jsonfile /tmp/global.json --install-dir /usr/share/dotnet \
 && rm /tmp/dotnet-install.sh
ENV PATH="${PATH}:/usr/share/dotnet"
ENV DOTNET_ROOT=/usr/share/dotnet
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1

# Layer B — NuGet restore. Rebuilds only when csproj / CPM / global.json change.
WORKDIR /workspace
COPY Directory.Packages.props Directory.Build.props global.json /workspace/
COPY NuGet.config* /workspace/
COPY build/_build/Build.csproj /workspace/build/_build/
COPY build/_build/packages.lock.json /workspace/build/_build/
COPY build/_build.Tests/Build.Tests.csproj /workspace/build/_build.Tests/
COPY build/_build.Tests/packages.lock.json /workspace/build/_build.Tests/
RUN dotnet restore /workspace/build/_build/Build.csproj --locked-mode

# Layer C — repo source COPY. Rebuilds on every code change.
# .dockerignore excludes bin/, obj/, vcpkg_installed/, .vs/, artifacts/, etc.
COPY . /workspace
WORKDIR /workspace

# Smoke check — log resolved versions for audit trail on every image build.
RUN echo "=== dotnet ===" \
 && dotnet --version \
 && echo "=== global.json ===" \
 && cat /workspace/global.json \
 && echo "=== container digest captured at runtime via CONTAINER_DIGEST env var ==="

ENTRYPOINT ["dotnet"]
CMD ["run", "--project", "build/_build", "--", "--target", "GenerateBindings"]
```

- [ ] **Step 3: Extend or create root-level .dockerignore**

If a repo-root `.dockerignore` does not exist, create it. If one exists, extend it. Add (if not already present):

```text
# Build outputs
**/bin/
**/obj/
**/.vs/
artifacts/
.cake-host/

# IDE / local
.idea/
.vscode/
*.user

# Docker
**/docker/.dockerignore

# Native build state — vcpkg_installed is generated INSIDE the container
# during EnsureVcpkgDependencies; never COPY the host's triplet output.
vcpkg_installed/
external/vcpkg/buildtrees/
external/vcpkg/packages/
external/vcpkg/downloads/

# Generated binding preview output — write-only volume target
artifacts/generated-bindings-preview/

# Git internals already excluded by buildx; explicit for clarity.
.git/
```

- [ ] **Step 4: Verify the Dockerfile syntax**

Run: `docker buildx build --check -f docker/binding-generator.Dockerfile .` (if buildx check is available)

Or run a dry build with placeholder ARG:
```pwsh
docker build --build-arg BASE_IMAGE=ubuntu:20.04 --no-cache=false -f docker/binding-generator.Dockerfile -t janset-binding-generator-syntax-check .
```

Expected: Layers build successfully. Note that the toolchain smoke check in the linux-builder image WON'T run because we're using `ubuntu:20.04` directly here as a syntax check — actual usage substitutes the linux-builder image at runtime.

Cleanup: `docker rmi janset-binding-generator-syntax-check`

- [ ] **Step 5: Commit (await approval)**

Proposed message:
```
feat(docker): add binding-generator.Dockerfile + .dockerignore for Stage 1 loop

Multi-layer Dockerfile derives from manifest.runtimes[linux-x64].container_image
via ARG BASE_IMAGE (single source of truth per AGENTS.md). Three layers:
  A) .NET SDK via dotnet-install.sh, pinned to global.json
  B) NuGet restore (--locked-mode), invalidated only by csproj/CPM/global.json
  C) repo source COPY, the only layer that rebuilds per code-edit iteration

.dockerignore excludes bin/, obj/, vcpkg_installed/, artifacts/, .vs/,
.cake-host/, external/vcpkg/{buildtrees,packages,downloads}/, .git/ —
keeps build context small and prevents host MSBuild/vcpkg state from
entering the container (per feedback_cross_os_container_mount memory).

Image is local-build-only (no GHCR push for Stage 1). Run-time bind-mounts:
output dir + named janset-vcpkg-cache volume.

Refs: spec §3.1, §4.3, §8.5
```

---

## Task 10: Add tools.cs generate-bindings Subcommand

**Files:**
- Modify: `tools.cs`

Add `GenerateBindingsCommand` to the existing Spectre.Console.Cli surface. Reads `build/manifest.json` to resolve `BASE_IMAGE`, orchestrates `docker pull → inspect → build → volume → run` via CliWrap, streams container output to AnsiConsole.

- [ ] **Step 1: Locate where commands are registered in tools.cs**

Read `tools.cs:31-49` — the `CommandApp` is configured with `BuildCommand`, `SetupCommand`, `CiSimCommand`. Add `GenerateBindingsCommand` to that list.

- [ ] **Step 2: Add the GenerateBindingsCommand class to tools.cs**

Append the following inside the `// Commands` section of `tools.cs` (after `CiSimCommand`, before the `// Helpers` / `Shared` section):

```csharp
// ──────────────────────────────────────────────────────────────────
// GenerateBindings command
// ──────────────────────────────────────────────────────────────────

public sealed class GenerateBindingsSettings : CommandSettings
{
    [CommandOption("--rebuild-image")]
    [Description("Pass --no-cache to docker build, forcing full image rebuild.")]
    [DefaultValue(false)]
    public bool RebuildImage { get; init; }

    [CommandOption("--no-cache")]
    [Description("Skip the vcpkg binary cache volume; forces full vcpkg install inside container.")]
    [DefaultValue(false)]
    public bool NoCache { get; init; }
}

public sealed class GenerateBindingsCommand : AsyncCommand<GenerateBindingsSettings>
{
    private const string ImageTag = "janset-binding-generator:focal-latest";
    private const string VolumeName = "janset-vcpkg-cache";
    private const string OutputRelativePath = "artifacts/generated-bindings-preview/sdl2-core";

    protected override async Task<int> ExecuteAsync(CommandContext ctx, GenerateBindingsSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var repoRoot = Shared.GetRepoRoot();

        // [1] Validate docker daemon reachable.
        var dockerReady = await TryRunAsync(
            "docker", ["version", "--format", "{{.Server.Version}}"], cancellationToken).ConfigureAwait(false);
        if (dockerReady.ExitCode != 0)
        {
            AnsiConsole.MarkupLine("[red]Docker daemon not reachable.[/] Start Docker Desktop / dockerd, then retry.");
            return 66;
        }
        AnsiConsole.MarkupLine($"[green]Docker daemon ready[/] (server {dockerReady.StdOut.Trim()}).");

        // [2] Resolve BASE_IMAGE from build/manifest.json runtimes[linux-x64].container_image.
        var baseImage = ReadLinuxBaseImage(repoRoot);
        if (baseImage is null)
        {
            AnsiConsole.MarkupLine("[red]Could not resolve BASE_IMAGE from build/manifest.json runtimes[linux-x64].container_image.[/]");
            return 1;
        }
        AnsiConsole.MarkupLine($"[blue]BASE_IMAGE:[/] {baseImage}");

        // [3] docker pull ${BASE_IMAGE} to refresh mutable tag.
        AnsiConsole.MarkupLine("[blue]Pulling base image…[/]");
        var pullResult = await StreamRunAsync("docker", ["pull", baseImage], cancellationToken).ConfigureAwait(false);
        if (pullResult != 0)
        {
            AnsiConsole.MarkupLine($"[red]docker pull failed with exit code {pullResult}.[/]");
            return pullResult;
        }

        // [4] docker inspect to capture resolved digest for audit trail.
        var inspect = await TryRunAsync(
            "docker", ["inspect", "--format={{index .RepoDigests 0}}", baseImage], cancellationToken).ConfigureAwait(false);
        var containerDigest = inspect.StdOut.Trim();
        if (string.IsNullOrWhiteSpace(containerDigest))
        {
            AnsiConsole.MarkupLine("[yellow]Could not capture base image digest; continuing without audit-trail digest.[/]");
            containerDigest = string.Empty;
        }
        else
        {
            AnsiConsole.MarkupLine($"[blue]Base image digest:[/] {containerDigest}");
        }

        // [5] docker build the derived binding-generator image.
        AnsiConsole.MarkupLine("[blue]Building binding-generator image…[/]");
        var buildArgs = new List<string>
        {
            "build",
            "-t", ImageTag,
            "--build-arg", $"BASE_IMAGE={baseImage}",
            "-f", "docker/binding-generator.Dockerfile",
        };
        if (settings.RebuildImage)
        {
            buildArgs.Add("--no-cache");
        }
        buildArgs.Add(".");
        var buildExit = await StreamRunAsync("docker", [.. buildArgs], cancellationToken, workingDirectory: repoRoot).ConfigureAwait(false);
        if (buildExit != 0)
        {
            AnsiConsole.MarkupLine($"[red]docker build failed with exit code {buildExit}.[/]");
            return buildExit;
        }

        // [6] Create vcpkg binary cache volume (idempotent).
        if (!settings.NoCache)
        {
            await TryRunAsync("docker", ["volume", "create", VolumeName], cancellationToken).ConfigureAwait(false);
        }

        // [7] Ensure host bind-mount target exists.
        var outputDir = Path.Combine(repoRoot, OutputRelativePath);
        Directory.CreateDirectory(outputDir);

        // [8] docker run.
        AnsiConsole.MarkupLine("[blue]Running GenerateBindings target inside container…[/]");
        var runArgs = new List<string>
        {
            "run", "--rm",
            "-v", $"{outputDir}:/output",
            "-e", $"CONTAINER_DIGEST={containerDigest}",
        };
        if (!settings.NoCache)
        {
            runArgs.AddRange([
                "-v", $"{VolumeName}:/vcpkg-cache",
                "-e", "VCPKG_DEFAULT_BINARY_CACHE=/vcpkg-cache",
                "-e", "VCPKG_BINARY_SOURCES=files,/vcpkg-cache,readwrite",
            ]);
        }
        runArgs.AddRange([
            ImageTag,
            "run", "--project", "build/_build", "--",
            "--target", "GenerateBindings",
        ]);

        var runExit = await StreamRunAsync("docker", [.. runArgs], cancellationToken).ConfigureAwait(false);
        if (runExit != 0)
        {
            AnsiConsole.MarkupLine($"[red]Container run exited with code {runExit}.[/]");
            return runExit;
        }

        AnsiConsole.MarkupLine($"[green]Generation complete.[/] Output: {outputDir}");
        return 0;
    }

    private static string? ReadLinuxBaseImage(string repoRoot)
    {
        var manifestPath = Path.Combine(repoRoot, "build", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty("runtimes", out var runtimes))
            {
                return null;
            }
            foreach (var runtime in runtimes.EnumerateArray())
            {
                if (runtime.TryGetProperty("rid", out var rid) &&
                    rid.GetString() == "linux-x64" &&
                    runtime.TryGetProperty("container_image", out var image))
                {
                    return image.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Manifest parse error:[/] {ex.Message}");
        }
        return null;
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> TryRunAsync(
        string executable, IEnumerable<string> args, CancellationToken cancellationToken, string? workingDirectory = null)
    {
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        var cmd = Cli.Wrap(executable)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr));
        if (workingDirectory is not null)
        {
            cmd = cmd.WithWorkingDirectory(workingDirectory);
        }
        var result = await cmd.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return (result.ExitCode, stdOut.ToString(), stdErr.ToString());
    }

    private static async Task<int> StreamRunAsync(
        string executable, IEnumerable<string> args, CancellationToken cancellationToken, string? workingDirectory = null)
    {
        var cmd = Cli.Wrap(executable)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => AnsiConsole.WriteLine(line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => AnsiConsole.WriteLine(line)));
        if (workingDirectory is not null)
        {
            cmd = cmd.WithWorkingDirectory(workingDirectory);
        }
        var result = await cmd.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }
}
```

- [ ] **Step 3: Add a Shared.GetRepoRoot() helper if it doesn't exist**

Read the existing `// Shared` section in `tools.cs`. If `Shared.GetRepoRoot()` is missing, add it near other shared helpers:

```csharp
public static string GetRepoRoot()
{
    var current = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(current))
    {
        if (File.Exists(Path.Combine(current, "Janset.SDL2.sln")))
        {
            return current;
        }
        var parent = Directory.GetParent(current)?.FullName;
        if (parent == current || parent is null)
        {
            break;
        }
        current = parent;
    }
    return Environment.CurrentDirectory;
}
```

- [ ] **Step 4: Register GenerateBindingsCommand in the app**

In the `app.Configure(config =>` block, add:

```csharp
config.AddCommand<GenerateBindingsCommand>("generate-bindings")
    .WithDescription("Generates Stage 1 SDL2.Core preview bindings inside the linux-builder container.");
```

after the existing `config.AddCommand<CiSimCommand>("ci-sim");` line.

- [ ] **Step 5: Add `using System.Text;` if missing**

The new code references `StringBuilder`. Check the top of `tools.cs` — if `using System.Text;` is absent, add it.

- [ ] **Step 6: Verify tools.cs compiles**

Run: `dotnet run --file tools.cs -- --help`

Expected: Help output includes the new `generate-bindings` command alongside `build`, `setup`, `ci-sim`.

- [ ] **Step 7: Commit (await approval)**

Proposed message:
```
feat(tools): add `tools.cs generate-bindings` subcommand for Stage 1 scratch loop

Spectre.Console.Cli command orchestrates the Docker-based generation loop:
  1. validates Docker daemon
  2. resolves BASE_IMAGE from build/manifest.json runtimes[linux-x64]
  3. docker pull + docker inspect → captures resolved digest for audit
  4. docker build with --build-arg BASE_IMAGE=...
  5. docker volume create janset-vcpkg-cache (idempotent)
  6. docker run with bind-mounted output + vcpkg cache volume
  7. streams container stdout/stderr live; propagates exit code

Flags:
  --rebuild-image: passes --no-cache to docker build
  --no-cache:      skips vcpkg binary cache volume

CliWrap throughout; no Docker.DotNet dependency added.

Refs: spec §4.1, §5.1
```

---

## Task 11: Manual Smoke Test + Output Inspection

**Files:** None (manual verification step)

This task verifies the end-to-end loop produces sane output. It is **not** a commit step — it is the canonical Stage 1 acceptance test for this slice.

- [ ] **Step 1: Run the loop**

```pwsh
dotnet run --file tools.cs -- generate-bindings
```

Expected behavior:
- Docker daemon check passes.
- Base image pull succeeds.
- Docker build runs: Layer A SDK install ~3-4 min on first run, Layer B NuGet restore ~20s, Layer C source COPY ~10s.
- Volume create idempotent.
- Container starts; `EnsureVcpkgDependencies` runs vcpkg install (~10-15 min cold, ~30s after binary cache hit).
- `GenerateBindings` target logs:
  - "libclang resolved version: clang version 20.1.x..."
  - "linux-builder container digest: sha256:..."
  - "Resolved N SDL2 headers under '/workspace/vcpkg_installed/x64-linux-hybrid/include/SDL2'."
  - "Parsing view 'Neutral' (...)"
  - "Parsing view 'WindowsDesktop' (...)" through 'Android'
  - "View Neutral: K functions." through "View Android: K functions."
  - "Wrote 9 files to '/output'."
- Host AnsiConsole prints "Generation complete. Output: <path>"

- [ ] **Step 2: Inspect the generated output**

```pwsh
Get-ChildItem -Recurse artifacts/generated-bindings-preview/sdl2-core/
```

Expected:
```text
artifacts/generated-bindings-preview/sdl2-core/
├── Platform/
│   ├── Neutral/Commands.g.cs
│   ├── WindowsDesktop/Commands.g.cs
│   ├── WinRT/Commands.g.cs
│   ├── GDK/Commands.g.cs
│   ├── Linux/Commands.g.cs
│   ├── MacOS/Commands.g.cs
│   ├── IOS/Commands.g.cs
│   └── Android/Commands.g.cs
└── parse-views.json
```

- [ ] **Step 3: Eyeball-diff against the binding-spike**

Compare:
```pwsh
code artifacts/generated-bindings-preview/sdl2-core/Platform/Neutral/Commands.g.cs
code tools/binding-spike/cppast-platform/bindings/Generated/SDL2.Platform.Neutral.g.cs
```

Confirm the shape matches: bare `[DllImport(LibName, ...)]` stubs, `LibName = "SDL2"`, `[SupportedOSPlatform]` only on platform views, `using System.Runtime.Versioning;` present where attribute used.

- [ ] **Step 4: Inspect parse-views.json**

```pwsh
Get-Content artifacts/generated-bindings-preview/sdl2-core/parse-views.json | Out-String
```

Expected: 8 views in array form, each with `Name`, `SupportedOSPlatform`, `FunctionCount`, `Functions` (list of {Name, SourceHeader}).

- [ ] **Step 5: Re-run for determinism check**

```pwsh
dotnet run --file tools.cs -- generate-bindings
# Compare timestamps + content
git status artifacts/generated-bindings-preview/   # ignored; just for diff visibility
```

Or, copy output to a temp dir first:
```pwsh
Copy-Item -Recurse artifacts/generated-bindings-preview/sdl2-core /tmp/sdl2-preview-run1
dotnet run --file tools.cs -- generate-bindings
diff -r /tmp/sdl2-preview-run1 artifacts/generated-bindings-preview/sdl2-core
```

Expected: byte-identical content (no diff). If diff is non-zero, investigate — possible causes: non-deterministic ordering bug in emitter, parser non-determinism, sysroot drift (Section 10.2 deferred deep-dive).

- [ ] **Step 6: Capture smoke pass evidence**

Note in the next commit message or a verification log: "Manual smoke pass 2026-05-XX: N functions in Neutral, N+M total across 8 views, byte-identical re-run."

**No commit step — this is verification only.**

### Task 11 Implementation Result (2026-05-16)

Smoke passed after **9 iterative container runs** that surfaced and resolved frictions not anticipated in the 2026-05-15 plan. Full friction log + retracted assumptions in [`../../binding-autogen/research/binding-autogen-spike-findings.md`](../../binding-autogen/research/binding-autogen-spike-findings.md) §11.

Final smoke evidence:

- **9 files produced** on host: 8 `Platform/<View>/Commands.g.cs` + `parse-views.json`.
- **Header set:** 88 vcpkg-installed SDL2 headers minus exclusion list (umbrella, scaffolding, satellite umbrellas, satellite prefix, GL convenience wrappers, GL sub-headers, test scaffolding) → 54–58 headers per view depending on transitive resolution.
- **Function counts:** Neutral 848, WindowsDesktop +8, WinRT +12, GDK +12, Linux +2, MacOS 0, IOS +4, Android +13. Total cross-view union ≈ 899.
- **Top sources** (Neutral): SDL_stdinc.h 132, SDL_video.h 97, SDL_render.h 81, SDL_joystick.h 58, SDL_gamecontroller.h 58.
- **Zero satellite leakage** across all 8 views (no `IMG_*`/`Mix_*`/`TTF_*`/`SDLNet_*`/`SDL2_*` emitted).
- **vs. SDL2-CS (506 distinct externs):** 307 gap audited — ~141 scope-skip (SDL_stdinc / SDL_atomic / SDL_assert omissions where .NET has managed equivalents), ~16 unintended emit (SDL_FORCE_INLINE inline helpers, addressed by Task 11.5), ~150 version-delta (SDL2-CS pinned to SDL 2.0.22 of Apr 2022; our pin SDL 2.32.10 spans 6 upstream feature releases).
- **Wall time:** ~30 min cold run (8 views × ~55 headers = ~440 libclang TU invocations).
- **Step 5 byte-identical determinism re-run skipped** because each smoke is ~30 min wall time and the maintainer chose to defer the determinism re-check to the same commit window that approves Task 11.5. Re-run remains a Task 13 acceptance gate.

---

## Task 11.5: AST Inline Filter + `-U__has_builtin` Restore + Dynapi Cross-Check Validator

**Files:**

- Modify: `build/_build/Targets/GenerateBindings/Model/CppAstToPreviewModel.cs` (translator filter)
- Modify: `build/_build/Targets/GenerateBindings/Parsing/CppAstParseRunner.cs` (`-U__has_builtin` restore with correct comment)
- New: `build/_build/Data/BindingGeneration/DynapiManifest.cs` (pure record — `IReadOnlySet<string> PublicSymbols` + source metadata)
- New: `build/_build/Data/BindingGeneration/DynapiManifestParser.cs` (pure — Watcom `.exports` text → `DynapiManifest`)
- New: `build/_build/Data/BindingGeneration/IDynapiManifestRepository.cs` + `DynapiManifestRepository.cs` (Cake-aware — ADR-003 contract-centric pattern; resolves manifest via vcpkg buildtree path then WebFetch fallback)
- New: `build/_build/Validation/BindingGeneration/BindingPublicApiCoherenceValidator.cs` (root `Validation/` per existing convention — `HybridStaticOverlayValidator` sibling; behavior-first name per AGENTS.md §"Guardrail IDs are not code names")
- Modify: `build/_build/Targets/GenerateBindings/BindingGenerationRunner.cs` (resolve manifest via repository, call validator post-emit)
- Modify: `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs` (register repository + validator)
- New: `build/_build.Tests/Unit/Data/BindingGeneration/DynapiManifestParserTests.cs`
- New: `build/_build.Tests/Unit/Data/BindingGeneration/DynapiManifestRepositoryTests.cs` (FakeCakeWorld + buildtree fixture)
- New: `build/_build.Tests/Unit/Validation/BindingGeneration/BindingPublicApiCoherenceValidatorTests.cs`
- Modify: `build/_build.Tests/Unit/Targets/GenerateBindings/Parsing/CppAstParseRunnerTests.cs` (re-add `-U__has_builtin` assertion)

This task closes three gaps surfaced after Task 11:

### Subtask 11.5.A: Translator AST Inline Filter

Background: 16 distinct `SDL_FORCE_INLINE` helpers leak into Neutral output (`SDL_RectEmpty`, `SDL_RectEquals`, `SDL_PointInRect`, `SDL_FRectEmpty`, `SDL_FRectEquals`, `SDL_FRectEqualsEpsilon`, `SDL_PointInFRect`, `SDL_memset4`, `SDL_memcpy4`, `SDL_size_add_overflow`, `SDL_size_mul_overflow`, `SDL_MostSignificantBitIndex32`, `SDL_HasExactlyOneBitSet32`, `SDL_SwapFloat`, `_SDL_size_add_overflow_builtin`, `_SDL_size_mul_overflow_builtin`). They are not exported by `libSDL2-2.0.so` — calling them would raise `EntryPointNotFoundException`. Peer state-of-the-art (Silk.NET `src/Core/Silk.NET.BuildTools/Cpp/Clang.cs:1347-1358`): hard-skip `FunctionDecl.IsInlined` cursors. CppAst analog: `function.Flags.HasFlag(CppFunctionFlags.Inline)`.

- [ ] **Step 1:** Add filter to `CppAstToPreviewModel.ExtractFunctions`:

```csharp
if (!IsSdl2Header(function.SourceFile)
    || string.IsNullOrWhiteSpace(function.Name)
    || function.Flags.HasFlag(CppFunctionFlags.Inline))
{
    continue;
}
```

- [ ] **Step 2:** Re-run smoke. Expect Neutral 848 → ~832 (16 fewer). Confirm none of the 16 known inline names appear in any view's emitted output. Confirm SDL_Swap16/32/64 (which leak only on MSVC; they're macros on Windows path) still absent.
- [ ] **Step 3:** Add a translator unit test that asserts inline filtering — synthetic `CppCompilation` mock with one inline and one extern, verify only extern emitted. Translator unit tests are not yet part of the slice's test surface (per spec §7.1); this is the first.

### Subtask 11.5.B: `-U__has_builtin` Restore with Correct Comment

Background: The flag was added to `BaseAdditionalArguments`, then retired as "redundant" mid-iteration, then proven correct after peer evidence (ppy/SDL3-CS `SDL_stdinc.rsp`). Real mechanism: `SDL_stdinc.h:127-131` defines `_SDL_HAS_BUILTIN(x)` via `#ifdef __has_builtin`; undefining the macro forces `_SDL_HAS_BUILTIN(x) → 0`, which skips `SDL_stdinc.h:822, 853` `_SDL_size_*_overflow_builtin` SDL_FORCE_INLINE declarations entirely. Defense-in-depth complement to Subtask 11.5.A's AST filter — kills 2 of the 16 leaks at parse time. Side-effect audit: no public-API declarations affected.

- [ ] **Step 1:** Add `-U__has_builtin` back to `CppAstParseRunner.BaseAdditionalArguments` with a comment block explaining the actual mechanism (SDL_stdinc.h lines + `_SDL_HAS_BUILTIN` macro, NOT the previously-incorrect "GCC intrinsic guards" claim).
- [ ] **Step 2:** Re-add the test assertion in `CppAstParseRunnerTests`: `options.AdditionalArguments.Contains("-U__has_builtin")`.
- [ ] **Step 3:** Re-run smoke. Confirm `_SDL_size_*_overflow_builtin` names no longer reach the AST (they should never appear in CppAst's `compilation.Functions`, even before the inline filter). The remaining 14 inline names continue to be filtered by Subtask 11.5.A.

### Subtask 11.5.C: Dynapi Cross-Check Validator

Background: SDL2 ships `src/dynapi/SDL2.exports` as the textual public-API manifest. Cross-platform single source of truth. Validator pattern: generator-emit ∩ manifest = OK; generator-emit \ manifest = false-positive (fail-closed); manifest \ generator-emit = false-negative (fail-closed). Peer evidence: ppy/SDL3-CS `check_generated_functions` uses identical pattern against SDL3's `sdl.json`.

**Design decisions (locked 2026-05-16 by maintainer):**

1. **Manifest source — repository abstraction (ADR-003 pattern).** Treat `SDL2.exports` as a build-host input contract, mirror the existing `ManifestRepository` / `VersionFileRepository` pattern under `Data/BindingGeneration/`:
   - **`DynapiManifest`** (pure record) — `IReadOnlySet<string> PublicSymbols` plus source metadata (resolved path, manifest origin enum: `VcpkgBuildtree` / `WebFetch`).
   - **`DynapiManifestParser`** (pure) — Watcom `.exports` text → `DynapiManifest`. Regex `^\+\+'_(\w+)'\.'SDL2\.dll'\.'\w+'$`; comment / blank lines skipped.
   - **`IDynapiManifestRepository` + `DynapiManifestRepository`** (Cake-aware) — `LoadAsync(string sdl2Version, CancellationToken)` resolves vcpkg buildtree first (`external/vcpkg/buildtrees/sdl2/src/<sha>/src/dynapi/SDL2.exports`), falls back to WebFetch (`https://raw.githubusercontent.com/libsdl-org/SDL/release-<version>/src/dynapi/SDL2.exports`) when buildtree absent. Returns `Result<DynapiManifest, ManifestResolutionError>` so callers can decide fail-closed vs warn.

2. **Validator placement — root `Validation/BindingGeneration/` directly.** Per existing build-host convention (`HybridStaticOverlayValidator`, `BindingVcpkgCoherenceValidator`, `BindingSymbolExistenceValidator` all sit under `build/_build/Validation/`). Second-consumer promotion criterion already met because Stage 2 PreFlight + Pack stages are on the plan. Name: `BindingPublicApiCoherenceValidator` (behavior-first per AGENTS.md §"Guardrail IDs are not code names"). Public surface: `ValidationReport Validate(IReadOnlySet<string> emittedSymbols, DynapiManifest manifest, SeverityProfile profile)`.

3. **Severity profile.** `SeverityProfile` enum / record carries fail-closed thresholds per stage:
   - `Stage1Generator` — fail-closed on false-positives (sızıntı), log false-negatives as warnings (Stage 1 iterative tuning of exclusion list / parse views is expected).
   - `Stage2PreFlight` and `Stage2Pack` — fail-closed in both directions. Picked up by their respective task implementations when those slices land; Task 11.5 only wires `Stage1Generator`.

- [ ] **Step 1:** Implement `DynapiManifest` pure record under `build/_build/Data/BindingGeneration/`.
- [ ] **Step 2:** Implement `DynapiManifestParser` (pure) under same namespace; unit tests covering: simple `++'_FuncName'.'SDL2.dll'.'FuncName'`, comment lines (`#`), Watcom-conditional-disabled lines (`# ++`), blank lines, malformed entries (rejected with diagnostic).
- [ ] **Step 3:** Implement `IDynapiManifestRepository` + `DynapiManifestRepository` (Cake-aware) under same namespace. Probe order: vcpkg buildtree path → WebFetch. Unit tests use `FakeCakeWorld` for the buildtree branch; WebFetch branch is integration-only (don't unit-test network calls).
- [ ] **Step 4:** Implement `BindingPublicApiCoherenceValidator` under `build/_build/Validation/BindingGeneration/`. `SeverityProfile` record with the three profiles above. Method returns `ValidationReport` per existing `Validation/` shape. Unit tests assert FP / FN classification + severity threshold behaviour.
- [ ] **Step 5:** Wire `BindingGenerationRunner.RunAsync` to:
  - resolve `SDL2.exports` via `IDynapiManifestRepository.LoadAsync` (the runner already knows the vcpkg pin via `Sdl2CoreGenerationConfig` and the buildtree path via `IPathService`),
  - collect emitted symbol names from the final `PreviewBindingModel`,
  - invoke `BindingPublicApiCoherenceValidator.Validate(...) with SeverityProfile.Stage1Generator`,
  - on fail-closed `ValidationReport`, throw `CakeException` BEFORE writing files (don't leave half-validated output on disk).
- [ ] **Step 6:** Register both `IDynapiManifestRepository` and `BindingPublicApiCoherenceValidator` in `ServiceCollectionExtensions.AddGenerateBindings()`. Add the registrations to `ServiceCollectionExtensionsSmokeTests.AddGenerateBindings_Should_Register_All_Collaborator_Types`.
- [ ] **Step 7:** Re-run smoke. Expected outcome: zero false-positives after Subtasks 11.5.A + 11.5.B; false-negative warnings (if any) annotate the run log and feed back into exclusion-list / view-defines tuning.

### Subtask 11.5.D: Final Determinism Check

- [ ] Re-run `tools.cs generate-bindings` twice; diff outputs; expect byte-identical content. This is Task 11 Step 5 deferred — defense-in-depth dynapi validator and inline filter both have to land before the determinism re-check is meaningful.

### Open follow-ups (recorded 2026-05-16, NOT in Task 11.5 scope)

- **Container CPU allocation tuning** — `tools.cs generate-bindings` currently bottlenecks on a single container core (~30 min wall time, 100% one-core in container vs. ~3% host). Expose a `--cpus <N>` flag (Docker passthrough) and document operator-side `.wslconfig processors=N` setting; consider parallelising the outer per-view loop after a CppAst thread-safety audit. Tracked in [`../specs/2026-05-15-binding-generator-local-output-loop-design.md`](../specs/2026-05-15-binding-generator-local-output-loop-design.md) §10.3. Likely Task 6 or Task 13 implementation window.

**Commit step:** Single combined commit for Task 11.5 A+B+C+D together with the Task 1-11 dirty surface. Present summary + proposed message to Deniz; wait for explicit "go / apply / proceed / başla / yap"; then commit.

---

## Task 12: Documentation Updates

**Files:**
- Modify: `docs/binding-autogen/binding-autogen-strategy-brief.md`
- Modify: `docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md`
- Modify: `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`
- Modify: `docs/playbook/binding-generator-maintenance.md`
- Modify: `docs/binding-autogen/README.md`
- Modify: `.gitignore`

All updates per spec §9. Tasks 1-3 of the parent Stage 1 plan have already modified several of these docs; **append** the slice-specific content rather than overwriting existing edits.

- [ ] **Step 1: Update .gitignore**

Append to `.gitignore`:

```text
# Stage 1 binding-autogen scratch loop output (gitignored until production-location flag-flip at Task 7)
artifacts/generated-bindings-preview/
```

Skip this step if `artifacts/` is already a blanket gitignore entry covering generated-bindings-preview transitively.

- [ ] **Step 2: Update docs/binding-autogen/binding-autogen-strategy-brief.md**

In §Plan Shape (or the equivalent stage breakdown section), add a sentence after the Stage 1 description:

> "Stage 1 begins with a scratch output loop landing under `artifacts/generated-bindings-preview/sdl2-core/`; production-location flag-flip to `src/SDL2.<Family>/Generated/` lands at Stage 1 Task 7. Local invocation: `dotnet run --file tools.cs -- generate-bindings`. Linux-canonical Docker container is a hard prerequisite. See [`docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md`](../superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md)."

- [ ] **Step 3: Update docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md**

In §Documentation and Sequencing, add a sub-paragraph before the existing "1. Add the SDL2 generator module" list:

> "**Precursor slice (2026-05-15):** A narrow local-output loop landing before Task 4 establishes the iteration surface for model + emitter design. See [`2026-05-15-binding-generator-local-output-loop-design.md`](2026-05-15-binding-generator-local-output-loop-design.md)."

- [ ] **Step 4: Update docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md**

Insert a new task between current Task 3 and Task 4. Renumber subsequent tasks (4 → 5, 5 → 6, etc.), or use "Task 3.5" without renumbering if downstream task references are extensive.

The new task's content can be a one-paragraph cross-reference plus a link:

```markdown
## Task 3.5: Local Output Loop (precursor)

> See [`docs/superpowers/plans/2026-05-15-binding-generator-local-output-loop.md`](2026-05-15-binding-generator-local-output-loop.md) for the full implementation plan and [`docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md`](../specs/2026-05-15-binding-generator-local-output-loop-design.md) for the design rationale.

This precursor slice establishes the Docker-based local generation loop (Cake `GenerateBindings` target + `tools.cs generate-bindings` subcommand + derived `binding-generator.Dockerfile`) so subsequent model + emitter tasks (4-6) iterate against real CppAst-output artifacts under `artifacts/generated-bindings-preview/sdl2-core/`. Output is gitignored at this stage; production-location flag-flip to `src/SDL2.<Family>/Generated/` lands at Task 7.
```

- [ ] **Step 5: Update docs/playbook/binding-generator-maintenance.md**

Add four sections (or extend existing equivalents):

(a) **Trio Pinning** — version mapping table:

```markdown
## Trio Pinning

The CppAst + libclang.runtime + libClangSharp.runtime trio moves as a coordinated set per ADR-004. Mismatches surface as runtime stack overflows during AST visit (see `binding-autogen-spike-findings.md §7.2 friction #5`).

| CppAst | libclang.runtime.* | libClangSharp.runtime.* | Status |
|---|---|---|---|
| 0.24.0 (2025-11-20) | 20.1.2 | 20.1.2 | Stage 1 pinned |
| 0.25+ | TBD | TBD | candidates — see post-Stage-1 revalidation below |

Bump procedure:
1. Update all three versions in `Directory.Packages.props`.
2. Run `dotnet restore --force-evaluate` to refresh `packages.lock.json`.
3. Run `tools.cs generate-bindings` and inspect output for AST regressions.
4. Update this table.
5. Bump ADR-004 status notes if a major libclang version is involved.
```

(b) **Post-Stage-1 trio revalidation** — deferred maintenance item:

```markdown
## Post-Stage-1 Trio Revalidation

After Stage 1 Tasks 4-10 ship and the regeneration baseline is stable, attempt to bump the trio to absolute-latest available versions on NuGet. See [`docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md`](../superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md) §10.1 for trigger criteria and rationale (Deniz, 2026-05-15: "20.1.x içime sinmedi ama şimdilik concern'ümüz olmamalı").
```

(c) **Local generation loop** — operator commands:

```markdown
## Local Generation Loop

```pwsh
dotnet run --file tools.cs -- generate-bindings
dotnet run --file tools.cs -- generate-bindings --rebuild-image   # force docker layer cache invalidation
dotnet run --file tools.cs -- generate-bindings --no-cache        # skip vcpkg binary cache volume
```

vcpkg binary cache lives in a named Docker volume. To nuke and start fresh:
```pwsh
docker volume rm janset-vcpkg-cache
```

Output lands at `artifacts/generated-bindings-preview/sdl2-core/` (gitignored). Production-location flag-flip to `src/SDL2.<Family>/Generated/` is a Stage 1 Task 7 concern, not a maintenance operation.
```

(d) Existing **Maintenance Checklist** — append two items:

```markdown
- [ ] Trio version table reviewed for CppAst bump candidacy.
- [ ] Manual `tools.cs generate-bindings` smoke after maintenance change.
```

- [ ] **Step 6: Update docs/binding-autogen/README.md**

In the Stage 1 status block, append:

> "**Local loop (precursor, Stage 1 Task 3.5):** `dotnet run --file tools.cs -- generate-bindings` runs the Cake `GenerateBindings` target inside the pinned `linux-builder` derived container, producing spike-style preview output under `artifacts/generated-bindings-preview/sdl2-core/`. See [`docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md`](../superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md)."

- [ ] **Step 7: ~~Update docs/parking-lot.md~~ — RETIRED 2026-05-16**

Binding-autogen-internal deferrals are tracked in [`../specs/2026-05-15-binding-generator-local-output-loop-design.md`](../specs/2026-05-15-binding-generator-local-output-loop-design.md) §10 (canonical) and in [`2026-05-14-sdl2-core-binding-generator-stage-1.md`](2026-05-14-sdl2-core-binding-generator-stage-1.md) when Stage-1-staged. `docs/parking-lot.md` is reserved for items outside the binding-autogen package scope. No parking-lot update for this slice.

- [ ] **Step 8: Build managed projects to confirm doc changes don't break links**

Run: `dotnet build build/_build/Build.csproj -c Release`

Expected: PASS. Docs aren't compiled, but this catches accidental code-side regressions from cross-cutting doc updates.

- [ ] **Step 9: Commit (await approval)**

Proposed message:
```
docs(binding-autogen): Stage 1 local output loop — slice doc updates

Cross-references the precursor slice (Task 3.5) into:
  - strategy brief (Plan Shape)
  - architecture spec (Documentation and Sequencing)
  - Stage 1 plan (new Task 3.5)
  - maintenance playbook (Trio Pinning table + post-Stage-1 revalidation
    item + local loop operator section + checklist additions)
  - binding-autogen README (Stage 1 status)

.gitignore extends to cover artifacts/generated-bindings-preview/.

Refs: spec §9
```

---

## Task 13: Final Verification

**Files:** None (verification step)

End-to-end gate before the slice is considered complete.

- [ ] **Step 1: Full build**

Run: `dotnet build build/_build/Build.csproj -c Release`

Expected: PASS, no warnings (or only existing warnings — no new ones introduced).

- [ ] **Step 2: Full test suite**

Run: `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0`

Expected: PASS. Test count grows from 640 (Stage 1 Tasks 1-3 baseline) by ~10-13 tests:
- PathServiceTests: +3
- PreviewEmitterTests: +8
- PreviewParseViewReportTests: +1
- GenerateBindingsTaskScenarioTests: +3
- ServiceCollectionExtensionsSmokeTests: +1 added test

Approx new total: ~656 passing, ~2 skipped.

- [ ] **Step 3: Anti-slop gate**

Run: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"`

Expected: clean (no new warnings). If new warnings surface, investigate per AGENTS.md §"Skills Used in This Project" — slopwatch is a hard gate.

- [ ] **Step 4: Re-run the manual smoke**

Run: `dotnet run --file tools.cs -- generate-bindings`

Expected: PASS — produces output, byte-identical to previous run.

- [ ] **Step 5: Diff confirmation**

Run: `git status --short`

Expected: New/modified files match the File Structure section at the top of this plan. `artifacts/generated-bindings-preview/` should NOT appear (gitignored).

- [ ] **Step 6: Present slice landing summary to Deniz**

Provide a one-paragraph summary suitable for a final commit message OR a "slice complete, ready for next slice" report:

```
Stage 1 binding-autogen local output loop slice landed:
  - GenerateBindings Cake target depending on EnsureVcpkgDependencies
  - Pure model + translator + emitter under Targets/GenerateBindings/{Model,Emitting}/
  - Cake-aware BindingGenerationRunner with libclang version assertion
  - Derived binding-generator.Dockerfile (multi-layer cache, ARG BASE_IMAGE from manifest)
  - tools.cs generate-bindings subcommand (CliWrap → docker pull/inspect/build/volume/run)
  - 15+ new unit + scenario tests; total 656 passing, 2 skipped
  - 8 .g.cs preview files + parse-views.json generated under artifacts/
  - Doc updates: strategy brief, architecture spec, Stage 1 plan, maintenance
    playbook, README, parking-lot, .gitignore
  - Two parking-lot deferrals: post-Stage-1 trio revalidation + sysroot deep-dive
  - Manual smoke pass: deterministic re-run produces byte-identical output

Ready for Stage 1 Task 4: real binding model + merge policy.
```

---

## Self-Review (executed inline during plan authoring)

1. **Spec coverage check (§ → task mapping):**
   - Spec §2.1 deliverables → Tasks 1-12 cover every row.
   - Spec §3.1 cross-OS mount constraint → Task 9 .dockerignore + Dockerfile COPY pattern.
   - Spec §4.1 file structure → Tasks 1-7, 9-10 enumerate every new/modified file.
   - Spec §4.2 PathService temp props → Task 1.
   - Spec §4.3 Dockerfile shape → Task 9.
   - Spec §4.4 BuildContext non-change → Task 6 confirms via task code (no Option/ParsedArguments touched).
   - Spec §4.5 Pure/Cake split → Tasks 2-4 (pure), 5-6 (Cake-aware).
   - Spec §5.1 host invocation flow → Task 10.
   - Spec §5.2 container invocation chain → Tasks 5, 6 (runner + task with EnsureVcpkgDependencies dep).
   - Spec §6 error handling → Task 5 (runner asserts) + Task 6 (task asserts) + Task 8 (scenario tests).
   - Spec §7 testing → Tasks 4 (emitter), 7 (smoke), 8 (scenario).
   - Spec §8.2 libclang version assertion → Task 5.
   - Spec §8.3 trio table → Task 12 §Trio Pinning.
   - Spec §8.4 stamp container.digest → deferred: spec notes this lands at Stage 1 Task 8 (PreFlight), not in this slice. Runner logs container digest from env var (Task 5) but does not write a stamp. ✓ aligned.
   - Spec §9 doc updates → Task 12.
   - Spec §10 deferred follow-ups → spec §10 itself is the canonical record (binding-autogen-internal deferrals live alongside the workstream, not in `docs/parking-lot.md`); maintenance playbook §"Post-Stage-1 Trio Revalidation" cross-references it.

2. **Placeholder scan:** No "TBD", "TODO", "implement later" left. The maintenance playbook's "0.25+ TBD | TBD" entry in the Trio Pinning table is correct (it's the canonical "not yet evaluated" marker, not a plan placeholder).

3. **Type consistency:**
   - `IBindingGenerationRunner.RunAsync` signature: `Task RunAsync(GenerateBindingsRequest request, CancellationToken cancellationToken)` — consistent in Task 5 definition, Task 6 caller, Task 8 stub.
   - `PreviewEmitter.Emit(PreviewBindingModel)` returns `GeneratedFileSet` — consistent in Task 4 definition, Task 5 caller.
   - `CppAstToPreviewModel.Translate(IReadOnlyList<CppAstParseResult>) → PreviewBindingModel` — consistent in Task 3, Task 5 caller.
   - `IPathService.GetGenerateBindingsPreviewFamilyRoot(string family)` — consistent in Task 1 definition, Task 6 caller.

4. **Ambiguity scan:** Caught one in Task 5 (Cake async-write extension vs `File.WriteAllTextAsync` fallback) — resolved with Step 2 instruction to verify before settling on the impl.

Plan complete.

---

**Plan saved to `docs/superpowers/plans/2026-05-15-binding-generator-local-output-loop.md`.**
