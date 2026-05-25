# ClangSharp Oracle Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `spikes/binding-generators/clangsharp/oracle.cs`, a .NET 10 file-based Roslyn oracle that reports family-aware raw ABI evidence for the ClangSharp spike.

**Architecture:** Keep `generate_bindings.py` as the ppy-style generation orchestrator. Add a standalone file-based C# validator that extracts structured C# evidence with Roslyn, combines it with manifest/dynapi/SDL2-CS/Cake-preview evidence, and renders a Markdown report. Do not modify production Cake, project files, manifests, CI, or package projects.

**Tech Stack:** .NET 10 file-based app, `Microsoft.CodeAnalysis.CSharp` 4.12.0, `System.Text.Json`, generated C# syntax analysis, Markdown report output.

---

## File Structure

**Create**

- `spikes/binding-generators/clangsharp/oracle.cs` — standalone file-based app. Owns CLI parsing, Roslyn extraction, family source discovery, raw ABI checks, and Markdown report rendering.

**Modify**

- `spikes/binding-generators/README.md` — add `clangsharp/oracle.cs` to the layout/quick command sections after implementation proves the command works.
- `spikes/binding-generators/docs/llm-handoff.md` — update only after the oracle command exists and the next agent would otherwise inherit stale context.
- `spikes/binding-generators/docs/next-iteration-plan.md` — update evidence snapshot only after the oracle command produces a useful report.

**Generated / Updated By Command**

- `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md` — report written by `oracle.cs --write-report`.

**Do Not Touch In This Plan**

- `spikes/binding-generators/clangsharp/generate_bindings.py`
- `build/manifest.json`
- Any `.csproj`, `.sln`, `.slnx`, `Directory.Build.props`, `Directory.Packages.props`
- Git commits. Per `AGENTS.md`, present summary and proposed commit message before any commit.

## Implementation Notes

- Use `#:property TargetFramework=net10.0` and `#:property PublishAot=false` at the top of `oracle.cs`.
- Use `#:package Microsoft.CodeAnalysis.CSharp@4.12.0`, matching the existing spike-only Roslyn version in `spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj`.
- Keep the app self-contained. Do not create a project file for the first slice.
- Use a built-in `--self-test` mode instead of adding test project infrastructure in the first slice.
- Keep native export extraction report-only / `not-wired` in this first slice.
- Retain the legacy Python oracle-comparison path only until `oracle.cs` has equal-or-better coverage. Item 1 retires that path once `oracle.cs` is the active evidence reporter.

## Task 1: Bootstrap `oracle.cs` With CLI And Self-Test Harness

**Files:**

- Create: `spikes/binding-generators/clangsharp/oracle.cs`

- [ ] **Step 1: Write the failing self-test harness**

Create `oracle.cs` with file-based directives, CLI parsing, a `--self-test` mode, test fixtures, and intentionally empty extraction results. Use the fixture shape below so the first run proves the self-test can catch missing extraction behavior.

```csharp
#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property PublishAot=false
#:package Microsoft.CodeAnalysis.CSharp@4.12.0

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var options = OracleOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine(OracleOptions.HelpText);
    return 0;
}

if (options.SelfTest)
{
    return SelfTests.Run();
}

Console.Error.WriteLine("oracle.cs currently supports only --self-test in Task 1.");
return 2;

internal sealed record OracleOptions(bool SelfTest, bool ShowHelp)
{
    public const string HelpText = "usage: dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- [--self-test] [--family <id>] [--write-report]";

    public static OracleOptions Parse(string[] args)
    {
        var selfTest = args.Contains("--self-test", StringComparer.OrdinalIgnoreCase);
        var help = args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase);
        return new OracleOptions(selfTest, help);
    }
}

internal static class SelfTests
{
    public static int Run()
    {
        var failures = new List<string>();
        var evidence = CSharpEvidenceExtractor.Extract("fixture.g.cs", Fixtures.MixedGeneratedSource, preprocessorSymbols: ["NET5_0_OR_GREATER"]);

        Expect(evidence.Functions.Count == 3, "extracts DllImport, LibraryImport, and non-import methods", failures);
        Expect(evidence.Functions.Any(f => f.ManagedName == "SDL_Init" && f.NativeLibrary == "SDL2" && f.ImportKind == "DllImport"), "extracts DllImport library and method", failures);
        Expect(evidence.Functions.Any(f => f.ManagedName == "IMG_Init" && f.NativeLibrary == "SDL2_image" && f.ImportKind == "LibraryImport"), "extracts LibraryImport library and method", failures);
        Expect(evidence.Constants.Any(c => c.Name == "SDL_INIT_VIDEO" && c.Kind == "Field"), "extracts const fields", failures);
        Expect(evidence.Constants.Any(c => c.Name == "SDL_HINT_RENDER_DRIVER" && c.Kind == "Property"), "extracts expression-bodied UTF-8 span properties", failures);
        Expect(evidence.Types.Any(t => t.Name == "SDL_RWops" && t.Kind == "Struct"), "extracts structs", failures);
        Expect(evidence.Types.Any(t => t.Name == "SDL_bool" && t.Kind == "Enum"), "extracts enums", failures);

        if (failures.Count == 0)
        {
            Console.WriteLine("self-test: PASS");
            return 0;
        }

        Console.Error.WriteLine("self-test: FAIL");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine("- " + failure);
        }

        return 1;
    }

    private static void Expect(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
}

internal static class Fixtures
{
    public const string MixedGeneratedSource = """
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL2
{
    public enum SDL_bool
    {
        SDL_FALSE = 0,
        SDL_TRUE = 1,
    }

    public unsafe partial struct SDL_RWops
    {
        public delegate* unmanaged[Cdecl]<SDL_RWops*, long> size;
    }

    public static unsafe partial class SDLNative
    {
        public const uint SDL_INIT_VIDEO = 0x00000020u;
        public static ReadOnlySpan<byte> SDL_HINT_RENDER_DRIVER => "SDL_RENDER_DRIVER"u8;

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_Init(uint flags);

        public static int ManagedHelper() => 42;
    }
}

namespace SDL2.Image
{
    public static unsafe partial class SDL_imageNative
    {
        [LibraryImport("SDL2_image", EntryPoint = "IMG_Init")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_Init(int flags);
    }
}
""";
}

internal sealed record CSharpEvidence(
    IReadOnlyList<FunctionEvidence> Functions,
    IReadOnlyList<ConstantEvidence> Constants,
    IReadOnlyList<TypeEvidence> Types);

internal sealed record FunctionEvidence(string ManagedName, string NativeLibrary, string ImportKind, string Accessibility, string ContainingType, string NamespaceName);
internal sealed record ConstantEvidence(string Name, string Kind, string ContainingType, string NamespaceName);
internal sealed record TypeEvidence(string Name, string Kind, string NamespaceName);

internal static class CSharpEvidenceExtractor
{
    public static CSharpEvidence Extract(string path, string source, string[] preprocessorSymbols)
        => new([], [], []);
}
```

- [ ] **Step 2: Run self-test and verify RED**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: exit code `1`, output contains `self-test: FAIL` and at least `extracts DllImport, LibraryImport, and non-import methods`.

- [ ] **Step 3: Implement minimal Roslyn extraction to pass self-test**

Replace `CSharpEvidenceExtractor.Extract` with syntax extraction that:

- Parses source with `CSharpParseOptions.Default.WithPreprocessorSymbols(preprocessorSymbols)`.
- Walks namespace declarations and type declarations recursively.
- Extracts method declarations, including non-import methods, `DllImport`, and `LibraryImport`.
- Extracts const/static fields and expression-bodied properties.
- Extracts enum and struct declarations.

Use syntax-only extraction for this task. Attribute arguments can be string-literal only in Task 1.

- [ ] **Step 4: Run self-test and verify GREEN**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: exit code `0`, output contains `self-test: PASS`.

## Task 2: Extract Generated Binding Evidence From Real Files

**Files:**

- Modify: `spikes/binding-generators/clangsharp/oracle.cs`

- [ ] **Step 1: Add real CLI options and source discovery self-test**

Extend `OracleOptions` with:

- `RepoRoot`
- `Families`
- `WriteReport`
- `Stdout`

Supported command forms:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

Add a self-test case for repeated `--family` parsing:

```csharp
var parsed = OracleOptions.Parse(["--family", "sdl2-core", "--family", "sdl2-image", "--write-report"]);
Expect(parsed.Families.SequenceEqual(["sdl2-core", "sdl2-image"]), "parses repeated --family options", failures);
Expect(parsed.WriteReport, "parses --write-report", failures);
```

- [ ] **Step 2: Run self-test and verify RED**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: exit code `1`, output contains `parses repeated --family options`.

- [ ] **Step 3: Implement CLI parsing and family source config**

Add family config records in `oracle.cs`:

```csharp
internal sealed record FamilyConfig(
    string FamilyId,
    string DisplayName,
    string ExpectedNamespace,
    string ExpectedRawClassName,
    string? CakePreviewRelativePath,
    string ClangSharpCompatRelativePath,
    string ClangSharpModernRelativePath,
    string? Sdl2CsRelativePath,
    bool UsesSdl2Dynapi);
```

Hardcode the first two family configs:

- `sdl2-core`
  - Expected namespace: `SDL2`
  - Expected raw class: `SDLNative`
  - Cake preview: `artifacts/generated-bindings-preview/sdl2-core`
  - ClangSharp compat: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat`
  - ClangSharp modern: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern`
  - SDL2-CS: `external/sdl2-cs/src/SDL2.cs`
  - Dynapi: enabled

- `sdl2-image`
  - Expected namespace: `SDL2.Image`
  - Expected raw class: `SDL_imageNative`
  - Cake preview: none
  - ClangSharp compat: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Compat`
  - ClangSharp modern: `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern`
  - SDL2-CS: `external/sdl2-cs/src/SDL2_image.cs`
  - Dynapi: disabled

- [ ] **Step 4: Run self-test and verify GREEN**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: exit code `0`, output contains `self-test: PASS`.

- [ ] **Step 5: Run against real output without report writing**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image
```

Expected: exit code `0`; console output lists both families and source statuses for ClangSharp Compat, ClangSharp Modern, Cake preview, SDL2-CS, and dynapi. No report file is written in this step.

## Task 3: Add Dynapi, Manifest, Cake Preview, And SDL2-CS Evidence Lanes

**Files:**

- Modify: `spikes/binding-generators/clangsharp/oracle.cs`

- [ ] **Step 1: Add self-test fixtures for non-C# evidence**

Add fixture methods in `SelfTests` for:

- SDL2 dynapi export parsing line: `++'_SDL_Init'.'SDL2.dll'.'SDL_Init'`
- Manifest required function/constant parsing from a minimal JSON fixture.
- SDL2-CS function extraction from a minimal C# fixture containing `[DllImport(nativeLibName)] public static extern int SDL_Init(uint flags);` and `const string nativeLibName = "SDL2";`.

Expected self-test assertions:

```csharp
Expect(DynapiParser.Parse(Fixtures.DynapiExports).Contains("SDL_Init"), "parses SDL2 dynapi exports", failures);
Expect(required.RequiredFunctions.Contains("SDL_Init"), "parses manifest required SDL.h functions", failures);
Expect(required.RequiredConstants.Contains("SDL_INIT_VIDEO"), "parses manifest required SDL.h constants", failures);
Expect(sdl2Cs.Functions.Any(f => f.ManagedName == "SDL_Init" && f.NativeLibrary == "SDL2"), "extracts SDL2-CS DllImport compatibility functions", failures);
```

- [ ] **Step 2: Run self-test and verify RED**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: exit code `1`, output contains at least `parses SDL2 dynapi exports`.

- [ ] **Step 3: Implement evidence lane parsers**

Implement:

- `DynapiParser.Parse(string text)` for SDL2 `.exports` third quoted field.
- `ManifestRequiredSurfaceReader.Read(repoRoot)` using `System.Text.Json` and only `library_manifests[].binding_generation.required_functions/required_constants` for SDL2 Core.
- Reuse `CSharpEvidenceExtractor` for SDL2-CS files.
- Reuse `CSharpEvidenceExtractor` for Cake preview `.cs` files, but label Cake as Core-only and generator-preview evidence.

- [ ] **Step 4: Run self-test and verify GREEN**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: exit code `0`, output contains `self-test: PASS`.

- [ ] **Step 5: Run against real sources**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image
```

Expected: exit code `0`; console output reports:

- `sdl2-core` has ClangSharp Compat, ClangSharp Modern, Cake preview, SDL2-CS, and dynapi available if local files exist.
- `sdl2-image` has ClangSharp Compat, ClangSharp Modern, SDL2-CS available if local files exist, Cake preview as missing/not-applicable, and dynapi as out-of-scope.

## Task 4: Implement Raw ABI Constitution Checks

**Files:**

- Modify: `spikes/binding-generators/clangsharp/oracle.cs`

- [ ] **Step 1: Add self-test fixture that contains known raw ABI violations**

Add a fixture using `Fixtures.MixedGeneratedSource` and expected checks:

```csharp
var checks = RawAbiChecks.Run(FamilyConfigs.Sdl2Core, evidence, RequiredSurface.Empty);
Expect(checks.Any(c => c.CheckId == "raw-abi-public-class"), "flags public raw ABI class", failures);
Expect(checks.Any(c => c.CheckId == "raw-abi-public-import"), "flags effectively public raw import methods", failures);
Expect(checks.Any(c => c.CheckId == "deferred-layout-sdl-rwops"), "flags SDL_RWops layout emission", failures);
```

Add a second fixture for Image namespace drift:

```csharp
Expect(imageChecks.Any(c => c.CheckId == "family-namespace-drift"), "flags SDL2_image namespace drift", failures);
```

- [ ] **Step 2: Run self-test and verify RED**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: exit code `1`, output contains `flags public raw ABI class`.

- [ ] **Step 3: Implement first-pass checks**

Add `RawAbiCheck`:

```csharp
internal sealed record RawAbiCheck(string CheckId, string Severity, string Classification, string Symbol, string Message, string SourcePath);
```

Implement these checks:

- `raw-abi-public-class`: raw class is public.
- `raw-abi-public-import`: raw import method is effectively public because its raw ABI container is public. Lexically public imports inside an internal container are not public API leaks.
- `required-function-missing`: manifest required function is absent from ClangSharp generated evidence for Core.
- `required-constant-missing`: manifest required constant is absent from ClangSharp generated evidence for Core.
- `deferred-layout-sdl-rwops`: `SDL_RWops` struct has fields/nested fields in ClangSharp output.
- `deferred-layout-sdl-syswminfo`: `SDL_SysWMinfo` struct has fields in ClangSharp output.
- `deferred-layout-sdl-syswmmsg`: `SDL_SysWMmsg` struct has fields in ClangSharp output.
- `platform-sensitive-long`: method return/parameter has `NativeTypeName("long")` with managed `int`, or `NativeTypeName("unsigned long")` with managed `uint`.
- `platform-sensitive-wchar`: method return/parameter has `NativeTypeName` containing `wchar_t` with managed `ushort*`.
- `family-namespace-drift`: generated namespace does not match family config expected namespace.

- [ ] **Step 4: Run self-test and verify GREEN**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: exit code `0`, output contains `self-test: PASS`.

- [ ] **Step 5: Run checks against current generated output**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image
```

Expected: exit code `0`; console output includes non-zero raw ABI check counts for current known issues. This command should not fail solely because it found known raw ABI issues; first-slice behavior is report-only.

## Task 5: Render Family-Aware Markdown Report

**Files:**

- Modify: `spikes/binding-generators/clangsharp/oracle.cs`
- Generate/update: `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`

- [ ] **Step 1: Add self-test for report renderer**

Add a renderer self-test that creates a small in-memory `OracleReport` with two families and asserts the Markdown contains:

```text
# ClangSharp Oracle Evidence
## Inputs
## Family: sdl2-core
## Family: sdl2-image
### Raw ABI Constitution Checks
Hard Bug
Evidence Missing
```

- [ ] **Step 2: Run self-test and verify RED**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: exit code `1`, output contains renderer assertion failure.

- [ ] **Step 3: Implement Markdown renderer**

Render sections:

- Inputs table.
- Per-family source status table.
- Per-family surface counts.
- Per-family raw ABI constitution checks grouped by classification.
- Per-family function matrix for generated vs Cake/SDL2-CS/dynapi availability.
- Evidence gaps.

Keep rows bounded for readability. Show all `Hard Bug` and `Likely Bug` checks. For large symbol sets, show counts plus the first 50 sorted symbols and a remaining count line.

- [ ] **Step 4: Run self-test and verify GREEN**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: exit code `0`, output contains `self-test: PASS`.

- [ ] **Step 5: Generate the report**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

Expected: exit code `0`; report exists at `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`.

- [ ] **Step 6: Inspect report contents**

Read `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md` and confirm it explicitly lists current known raw ABI gaps instead of presenting build-clean output as correctness.

Expected current check categories include:

- Public raw ABI leak.
- Missing `SDL.h` required functions/constants.
- Deferred layout violations.
- Platform-sensitive scalar risks.
- Image namespace drift.

## Task 6: Update Spike Docs And Run Verification

**Files:**

- Modify: `spikes/binding-generators/README.md`
- Modify if context changes: `spikes/binding-generators/docs/llm-handoff.md`
- Modify if evidence snapshot changes: `spikes/binding-generators/docs/next-iteration-plan.md`

- [ ] **Step 1: Update README command docs**

Add this quick command to `spikes/binding-generators/README.md` near the existing quick commands:

```pwsh
# Raw ABI oracle/evidence report (Roslyn, family-aware)
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

- [ ] **Step 2: Update handoff/plan only if stale**

If `oracle.cs` changes the next-agent state, update:

- `spikes/binding-generators/docs/llm-handoff.md` current status and commands.
- `spikes/binding-generators/docs/next-iteration-plan.md` evidence snapshot.

Do not duplicate the full report contents in docs. Link to `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`.

- [ ] **Step 3: Run self-tests**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --self-test
```

Expected: exit code `0`, output contains `self-test: PASS`.

- [ ] **Step 4: Run oracle report generation**

Run:

```pwsh
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

Expected: exit code `0`; report exists and includes both `sdl2-core` and `sdl2-image` sections.

- [ ] **Step 5: Run generated-output build truth gate**

Run:

```pwsh
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release
```

Expected: exit code `0`. This confirms the oracle work did not accidentally modify generated projects or break the existing compile evidence.

- [ ] **Step 6: Run whitespace check**

Run:

```pwsh
git diff --check
```

Expected: no output and exit code `0`.

- [ ] **Step 7: Run Slopwatch**

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
```

Expected: exit code `0`. If Slopwatch flags the file-based `#:package` directive, do not suppress broadly. Either justify a local spike-only suppression in the same style as `postprocess/Janset.SDL2.PostProcess.csproj` or ask Deniz whether to promote the package version into central package management.

- [ ] **Step 8: Present commit gate summary**

Do not commit. Present:

- Changed files.
- Verification commands and results.
- Known raw ABI issues reported by `oracle.cs`.
- Proposed commit message.

Ask for approval before any commit.

## Self-Review Checklist

- Spec coverage: Covers Roslyn file-based oracle, family-aware evidence, raw ABI checks, ppy boundary, and verification.
- Placeholder scan: No unfinished markers or unspecified error handling instructions.
- Type consistency: `OracleOptions`, `FamilyConfig`, `CSharpEvidence`, `FunctionEvidence`, `ConstantEvidence`, `TypeEvidence`, and `RawAbiCheck` names are used consistently across tasks.
- Scope check: First slice stays spike-local and does not modify production build, manifest, project files, CI, public typed API, or friendly layer.
