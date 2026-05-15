# SDL2 Core Binding Generator Stage 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Revised 2026-05-15.** The 2026-05-14 draft of this plan proposed a standalone `src/Janset.SDL2.Bindings.Generator/` console app, a separate `tests/Janset.SDL2.Bindings.Generator.Tests/` test project, multi-RID `libclang.runtime.*` pins, and used the Windows `x64-windows-hybrid` triplet for canonical headers. All four were retracted on 2026-05-15:
>
> 1. **Generator folded into the Cake build host** under `build/_build/Targets/GenerateBindings/`. No new `src/`-tree project; no new test project. Tests join `build/_build.Tests/`. Decision rationale lives in the revised strategy brief + ADR-002 + ADR-003.
> 2. **Linux-canonical** runtime trio. Only `libclang.runtime.linux-x64` + `libClangSharp.runtime.linux-x64` are pinned; the generator fails closed on any other host RID.
> 3. **Canonical triplet is `x64-linux-hybrid`** (Linux container's native target). All Windows-host CLI examples are replaced with their Linux-container equivalents; the local dev path is `tools.cs generate-bindings`, which spins up the `linux-builder` container automatically.
> 4. **`SDL_syswm.h` typed-union layout deferred to Stage 2.** Stage 1 emits `SDL_GetWindowWMInfo` as a function with opaque `nint`-shaped `SDL_SysWMinfo*` parameter. The typed union + ~15–20-type forward-declaration stub library + 64-byte layout lock are Stage 2 deliverables.
>
> Stage 1's production-shape proof now covers the full `PlatformCatalog` at function level (Neutral + Windows desktop + WinRT + GDK + Linux + macOS + iOS + Android — ~7–8 parse views, preprocessor-macro switching only — no `--target`, no mingw-w64, no Apple SDK). Stage 2 takes on the typed-union layout, satellite sweep, and `external/sdl2-cs` retirement; Stage 3 (SDL3) is gated on PD-7. The plan's task-by-task discipline (TDD, baseline checks, approval-gate commits) is unchanged.

**Goal:** Build the first production-shaped CppAst generator slice for `Janset.SDL2.Core` hosted inside the Cake build host, commit generated SDL2.Core source, and remove SDL2.Core's production compile dependency on `external\sdl2-cs\src\SDL2.cs`.

**Architecture:** Add the binding generator as a Cake-target home under `build\_build\Targets\GenerateBindings\` with pure (Cake-free) sub-namespaces for header resolution, parsing, model, emit, and stamp writing — and the Cake-aware shell (`GenerateBindingsTask`, `BindingGenerationRunner`, `ServiceCollectionExtensions`) on top. Add cross-cutting validators under `build\_build\Validation\BindingGeneration\`. The generator resolves canonical vcpkg SDL2 headers, runs deterministic neutral plus platform/backend CppAst parse views via preprocessor-macro switching, merges declarations into a fail-closed binding model, emits committed `src\SDL2.Core\Generated\*.g.cs` output plus a deterministic `.generated-stamp`, and is orchestrated through the Cake `GenerateBindings` target. Local invocation is `tools.cs generate-bindings`, which publishes the Cake host (Release), mounts the repo root + a host vcpkg cache into the `linux-builder` container, and runs the Cake target inside the container. Public SDL2.Core output keeps the current `SDL2` namespace and `SDL` static class entry point while moving handles to typed nested `readonly partial struct` wrappers.

**Tech Stack:** .NET 10 / C# 14, CppAst 0.24.0, libclang.runtime.linux-x64 + libClangSharp.runtime.linux-x64 20.1.2, Cake Frosting 6.1, TUnit + Microsoft.Testing.Platform, PublicApiGenerator + Verify snapshots, central package management, Docker (hard prerequisite for local invocation).

---

## Scope Decisions Locked By This Plan

- Stage 1 generates **SDL2.Core only**. SDL2 satellites remain on `external\sdl2-cs` until the Stage 2 plan.
- Stage 1 adds the Cake `GenerateBindings` target under `build\_build\Targets\GenerateBindings\` and a PreFlight stamp validator under `build\_build\Validation\BindingGeneration\`. **No new `src/`-tree project is created** — the generator lives inside the existing Cake build host. **No new test project is created** — tests live in `build\_build.Tests\`.
- Stage 1 adds `tools.cs generate-bindings` as the local invocation surface. It orchestrates a Docker run against the `linux-builder` container; Docker is a **hard prerequisite** and there is no host-OS fallback.
- Stage 1 does **not** add `.github\workflows\regenerate-bindings.yml`; that workflow belongs after the local/Cake generator path is real.
- Stage 1 does **not** add Pack-stage exported-symbol validation. It preserves exact native `EntryPoint` metadata in generated command declarations so that later Pack validation can verify emitted imports against harvested native exports.
- Generated SDL2.Core keeps `namespace SDL2; public static unsafe partial class SDL` so existing consumers still call `SDL.SDL_Init`, `SDL.SDL_Quit`, and related APIs. Opaque SDL pointers become nested typed handles such as `SDL.SDL_Window`.
- Stage 1 covers the **full `PlatformCatalog`** at function level (Neutral + Windows desktop + WinRT + GDK + Linux + macOS + iOS + Android). Each catalog entry is a `(OsCondition, BackendCondition[])` tuple; the number of parse views equals the catalog size (~7–8), **not** the size of the master "undefine-all-platform-macros" hygiene list. Niche backends (DirectFB, Vivante, MIR, OS/2) are documented exclusions for Stage 1; revisit only on consumer ask.
- Parse views use **preprocessor-macro switching only**. No `--target` cross-compile flag, no mingw-w64, no Apple SDK headers. SDL's public headers carry the cross-platform opaque-type forward declarations the parser needs (`typedef struct _NSWindow NSWindow;` etc.). Verified via the local CppAst spike at `tools/binding-spike/cppast/generator/Program.cs:42-72` and ppy/SDL3-CS WebFetch on 2026-05-15.
- **`SDL_syswm.h` is Stage 2, not Stage 1.** Stage 1 emits `SDL_GetWindowWMInfo` as a function with opaque `nint`-shaped `SDL_SysWMinfo*` parameter and records the typed-union deferral in `UnsupportedDeclarations.g.json` with category `deferred-to-stage-2`. Stage 2 introduces the ~15–20-type forward-declaration stub library and emits typed `SDL_SysWMinfo`/`SDL_SysWMmsg` with `[StructLayout(LayoutKind.Explicit, Size = 64)]` and platform branches at `[FieldOffset(0)]`. **Do not** treat `SDL_syswm.h` typed-union as a Stage 1 "first-class proof target" — that framing in the 2026-05-14 draft was wrong and has been retracted.
- Known C variadic functions are explicitly classified in `KnownUnsupportedDeclarationPolicy` and omitted from generated P/Invoke output with a generated audit entry. Any unclassified unsupported declaration fails generation.
- `.generated-stamp` is deterministic. It must not include wall-clock timestamps because the reproducibility gate requires clean regenerated diffs.
- Package references are added with `dotnet add <project> package <package> --version <version>`; do not hand-edit package references or central package versions. Only `linux-x64` runtime variants are pinned — `libclang.runtime.linux-x64` and `libClangSharp.runtime.linux-x64`. Non-Linux runtime packages are intentionally absent and the `GenerateBindings` target fails closed on non-`linux-x64` hosts with an actionable diagnostic.
- Commits in the steps are checkpoints. Because this repo has an approval gate, each commit step means: present the summary and proposed message to Deniz, wait for approval, then commit.

## Cross-References

This plan must be read alongside:

- [`docs/binding-autogen/binding-autogen-strategy-brief.md`](../../binding-autogen/binding-autogen-strategy-brief.md) — accepted strategy brief (revised 2026-05-15 in lockstep with this plan).
- [`docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md`](../specs/2026-05-14-binding-generator-architecture-design.md) — architecture design spec (revised 2026-05-15 in lockstep).
- [`docs/decisions/2026-05-14-binding-autogen-toolchain.md`](../../decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 toolchain decision.
- [`docs/decisions/2026-05-05-target-centric-build-host.md`](../../decisions/2026-05-05-target-centric-build-host.md) — ADR-002 target-centric build host pattern this plan follows.
- [`docs/decisions/2026-05-12-build-host-data-layer.md`](../../decisions/2026-05-12-build-host-data-layer.md) — ADR-003 contract-centric data layer (used by `Validation/BindingGeneration/`).
- [`AGENTS.md`](../../../AGENTS.md) §Build-Host Reference Pattern + §"Pure code stays pure" + §"Cake nativeness is a hard rule at build boundaries".
- [`docs/knowledge-base/extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) — collaborator extraction discipline.
- [`docs/knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — canonical TUnit/MTP test infra + `FakeCakeWorld` + `TargetTestHost`.
- [`docs/knowledge-base/release-guardrails.md`](../../knowledge-base/release-guardrails.md) — guardrail catalog (binding vcpkg coherence final G-ID assigned at Phase 6).

## File Structure Map

### New generator code (inside the Cake build host)

```text
build\_build\Targets\GenerateBindings\
|-- GenerateBindingsTask.cs                       (Cake task, [TaskName], orchestration + CakeException translation)
|-- GenerateBindingsRequest.cs                    (immutable target input contract)
|-- GenerateBindingsOptions.cs                    (CLI-derived options, family selector)
|-- FamilyGenerationConfig.cs                     (family identity + input headers + namespace/class)
|-- Sdl2CoreGenerationConfig.cs                   (Stage 1 SDL2.Core-specific config)
|-- BindingGenerationRunner.cs                    (Cake-side runner; invokes the pure emitter via named adapter)
|-- ServiceCollectionExtensions.cs                (AddGenerateBindings() — focused DI registration per ADR-002 §2.5)
|-- HeaderSet\                                    (PURE — no Cake; unit-testable as ordinary C#)
|   |-- HeaderSetResolver.cs
|   |-- HeaderSetFingerprint.cs
|   `-- HeaderSet.cs
|-- Parsing\                                      (PURE — no Cake)
|   |-- CppAstParseRunner.cs
|   |-- CppAstParseResult.cs
|   |-- PlatformCatalog.cs                         (data-driven (OsCondition, BackendCondition[]) tuple catalog)
|   |-- PlatformCondition.cs
|   |-- PlatformParseView.cs
|   `-- ParseDiagnosticFormatter.cs
|-- Model\                                        (PURE — no Cake)
|   |-- BindingDeclaration.cs
|   |-- BindingModel.cs
|   |-- BindingParameter.cs
|   |-- BindingTypeRef.cs
|   |-- DeclarationCollector.cs
|   |-- DeclarationMergePolicy.cs
|   |-- CoreOwnedTypeMap.cs
|   |-- KnownUnsupportedDeclarationPolicy.cs       (incl. SDL_SysWMinfo typed-union as `deferred-to-stage-2`)
|   `-- TypeMappingPolicy.cs
|-- Emitting\                                     (PURE — no Cake)
|   |-- CodeWriter.cs
|   |-- CsCodeGenerator.cs
|   |-- CsCommandEmitter.cs
|   |-- CsConstantEmitter.cs
|   |-- CsEnumEmitter.cs
|   |-- CsHandleEmitter.cs
|   |-- CsStructEmitter.cs
|   |-- CsCallbackEmitter.cs
|   `-- GeneratedFileSet.cs
`-- Stamps\                                       (PURE — no Cake)
    |-- GeneratedStamp.cs
    `-- GeneratedStampWriter.cs

build\_build\Validation\BindingGeneration\
|-- BindingGenerationCoherenceValidator.cs        (PreFlight: .generated-stamp drift)
|-- BindingGenerationStampContract.cs
|-- IBindingGenerationCoherenceValidator.cs
`-- BindingGenerationCoherenceReport.cs
```

There are **no `Stubs/` subdirectory** in Stage 1. Stage 1 parses against vcpkg-installed canonical headers + the Linux container's apt sysroot only — no hand-written platform stubs are needed for function-level surface (verified against ppy/SDL3-CS Dockerfile/`generate_bindings.py` on 2026-05-15 and the local CppAst spike at `tools/binding-spike/cppast/generator/Program.cs:42-72`). The Stage 2 plan adds a small forward-declaration stub library (~15–20 opaque types) for the `SDL_SysWMinfo`/`SDL_SysWMmsg` typed-union layout work.

There is **no `Cli/` subdirectory** in Stage 1. The 2026-05-14 draft proposed a separate `BindingGeneratorCli.cs` / `CommandLineParser.cs` because the generator was a standalone console app. The generator now runs inside the Cake host, so CLI arg parsing is the existing Cake/Frosting CLI surface — the `GenerateBindings` target consumes `--family` (and other options) through `BuildContext` properties just like every other target in `build\_build\Targets\<X>\`.

### New generator tests (inside the existing test project)

Tests join the canonical TUnit/MTP infrastructure under `build\_build.Tests\`. **No new `tests/Build.Targets.GenerateBindings.Tests/` test project is created** — the 2026-05-14 draft's separate-project proposal was retracted along with the standalone generator project.

```text
build\_build.Tests\Unit\Targets\GenerateBindings\
|-- HeaderSet\HeaderSetResolverTests.cs
|-- HeaderSet\HeaderSetFingerprintTests.cs
|-- Parsing\PlatformCatalogTests.cs                 (asserts ~7-8 catalog entries, OS + backend macro groups)
|-- Parsing\CppAstParseRunnerTests.cs               (asserts preprocessor-macro switching only, no --target)
|-- Model\DeclarationMergePolicyTests.cs
|-- Model\TypeMappingPolicyTests.cs
|-- Model\KnownUnsupportedDeclarationPolicyTests.cs  (asserts SDL_SysWMinfo typed-union is `deferred-to-stage-2`)
|-- Emitting\CsCodeGeneratorTests.cs
|-- Stamps\GeneratedStampWriterTests.cs              (asserts no wall-clock timestamps)
|-- Snapshots\Sdl2CorePublicApiTests.ApprovePublicApi.verified.txt
`-- Sdl2CorePublicApiTests.cs

build\_build.Tests\Scenarios\GenerateBindings\
|-- GenerateBindingsTaskScenarioTests.cs             (FakeCakeWorld + TargetTestHost — happy path, family selection, non-Linux host fail-closed)
|-- BindingGenerationCoherenceValidatorTests.cs      (stamp drift detection at PreFlight)
`-- Sdl2CoreGenerationConfigTests.cs
```

### New generated SDL2.Core output

```text
src\SDL2.Core\Generated\
|-- Commands.g.cs
|-- Constants.g.cs
|-- Enums.g.cs
|-- Handles.g.cs
|-- Structs.g.cs                                     (SDL_SysWMinfo emitted as opaque blob; typed union deferred to Stage 2)
|-- Callbacks.g.cs
|-- Marshalling.g.cs
|-- UnsupportedDeclarations.g.json                   (incl. `SDL_SysWMinfo` + `SDL_SysWMmsg` typed-union, category `deferred-to-stage-2`)
|-- PlatformAudit.g.json
|-- Platform\
|   |-- Windows\Commands.g.cs
|   |-- WinRT\Commands.g.cs
|   |-- GDK\Commands.g.cs
|   |-- Linux\Commands.g.cs
|   |-- MacOS\Commands.g.cs
|   |-- IOS\Commands.g.cs
|   `-- Android\Commands.g.cs
`-- .generated-stamp
```

Note: there is no `Platform\Backends\SDL_syswm.g.cs` in Stage 1. The typed-union file lands in Stage 2 once the forward-declaration stub library is in place.

### tools.cs orchestration additions

```text
tools.cs                                             (root-level file-based .NET 10 app — adds `generate-bindings` subcommand)
.dockerignore                                         (extended to exclude vcpkg_installed\, artifacts\, .vs\, bin\, obj\, .cake-host\)
```

### Existing files modified

- `Janset.SDL2.sln` — **no additions** for the generator itself; the generator lives inside the existing `build/_build/Build.csproj`. Add only what's truly new (none in Stage 1's generator scope).
- `build\_build\Build.csproj` — add `CppAst 0.24.0` + `libclang.runtime.linux-x64 20.1.2` + `libClangSharp.runtime.linux-x64 20.1.2` package references via `dotnet add build\_build\Build.csproj package ... --version ...`. **Non-Linux runtime variants are intentionally not pinned.**
- `Directory.Packages.props` — add central CPM versions for the three packages above via `dotnet add`; keep the CppAst/libclang trio coordinated at `0.24.0` / `20.1.2`. Document the Linux-canonical lock in a comment block.
- `src\SDL2.Core\SDL2.Core.csproj` — remove `external\sdl2-cs` compile include; SDK glob picks up `Generated\**\*.cs`; ensure `AllowUnsafeBlocks=true`.
- `build\_build\Program.cs` — add `--family` option to the Cake host CLI surface and register `AddGenerateBindings()`.
- `build\_build\BuildContext.cs` — surface `Family`, `OutputRoot`, and related GenerateBindings inputs as named, readonly CLI-derived properties per ADR-002 §2.3.
- `build\_build\Validation\ServiceCollectionExtensions.cs` — register `BindingGenerationCoherenceValidator` via `AddBindingGenerationValidation()` (or extend `AddValidation()`).
- `build\_build\Targets\PreFlightCheck\PreFlightCheckTask.cs` and its reporter — run and report binding-generation coherence.
- `build\_build.Tests\Unit\CompositionRoot\ServiceCollectionExtensionsSmokeTests.cs` — add `AddGenerateBindings` / validator smoke.
- `build\_build.Tests\Scenarios\PreFlightCheck\PreFlightCheckTaskScenarioTests.cs` — seed generated stamp fixture and assert drift failures.
- `tools.cs` — add `generate-bindings` subcommand (Spectre.Console.Cli command) that publishes the Cake host Release, mounts repo root + vcpkg cache, and runs the `GenerateBindings` target inside the `linux-builder` container.
- `.dockerignore` — extend to exclude `vcpkg_installed\`, `artifacts\`, `.vs\`, `bin\`, `obj\`, `.cake-host\` so the container's volume mount does not leak host state.
- `.github\workflows\release.yml` — provision the canonical SDL2 header tree before PreFlight so the stamp validator can recompute header fingerprints.
- `tests\smoke-tests\package-smoke\PackageConsumer.Smoke\PackageSmokeTests.cs` — switch Core smoke assertions from `IntPtr.Zero` to typed handle `IsNull` where Core generated handles are used.
- `tests\Sandbox\Program.cs` — same typed handle update.
- `docs\binding-autogen\README.md`, `docs\playbook\local-development.md`, and `docs\knowledge-base\release-guardrails.md` — document the Stage 1 generator command and active stamp guardrail.

---

## Task 1: Add Generator Packages and Scaffold the GenerateBindings Target Home

**Files:**

- Modify: `build\_build\Build.csproj` (via `dotnet add` for CppAst + libclang.runtime.linux-x64 + libClangSharp.runtime.linux-x64)
- Modify: `Directory.Packages.props` (via `dotnet add`; document the Linux-canonical lock with a comment block)
- Create: `build\_build\Targets\GenerateBindings\` folder with placeholder `ServiceCollectionExtensions.cs` and `GenerateBindingsRequest.cs`
- Create: `build\_build\Targets\GenerateBindings\Sdl2CoreGenerationConfig.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Sdl2CoreGenerationConfigTests.cs`

There is **no new csproj**, **no new sln entry**, and **no separate test project**. The generator code lives inside `build/_build/Build.csproj`; tests live inside `build/_build.Tests/Build.Tests.csproj`. This honors ADR-002 §2.1 (target-centric navigation under `Targets/<CakeTargetName>/`) and avoids the parallel orchestration stack a `src/`-tree console app would create.

- [ ] **Step 1: Run baseline checks**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
dotnet build src\SDL2.Core\SDL2.Core.csproj -c Release
```

Expected: both commands pass before Stage 1 changes. If a baseline command fails, stop and record the failure before editing.

- [ ] **Step 2: Add CppAst + Linux libclang runtime to the Cake build host**

Run (one at a time so `dotnet add` errors surface clearly):

```pwsh
dotnet add build\_build\Build.csproj package CppAst --version 0.24.0
dotnet add build\_build\Build.csproj package libclang.runtime.linux-x64 --version 20.1.2
dotnet add build\_build\Build.csproj package libClangSharp.runtime.linux-x64 --version 20.1.2
```

Expected: `Directory.Packages.props` gains three `PackageVersion` entries; `build/_build/Build.csproj` gains three `PackageReference` entries. **Do not add Windows or macOS runtime variants** — the generator is Linux-canonical. If `dotnet add` writes them into `Directory.Packages.props` unsorted, leave the sort as-is unless the file already had a sort convention; Stage 1 plan does not introduce a re-sort step.

After the adds, edit `Directory.Packages.props` to wrap the three new entries in a documented block:

```xml
<!--
  CppAst + libclang version trio — locked at 0.24.0 / 20.1.2. See ADR-004 and
  docs/binding-autogen/binding-autogen-strategy-brief.md §"Toolchain commitment + version-trio pin".
  Bump policy: all three packages move together; spike-validate against the new trio before merging.
  Linux-canonical lock: only the linux-x64 runtime is pinned. The GenerateBindings target fails
  closed on any other host RID; non-Linux runtime variants are intentionally absent.
-->
<PackageVersion Include="CppAst" Version="0.24.0" />
<PackageVersion Include="libclang.runtime.linux-x64" Version="20.1.2" />
<PackageVersion Include="libClangSharp.runtime.linux-x64" Version="20.1.2" />
```

- [ ] **Step 3: Create the GenerateBindings target folder + placeholder ServiceCollectionExtensions**

Create `build\_build\Targets\GenerateBindings\ServiceCollectionExtensions.cs` with the minimal DI registration shell that future tasks will extend:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.GenerateBindings;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGenerateBindings(this IServiceCollection services)
    {
        // Stage 1 tasks register pure collaborators here:
        //   HeaderSetResolver, CppAstParseRunner, PlatformCatalog, DeclarationMergePolicy,
        //   CsCodeGenerator, GeneratedStampWriter, BindingGenerationRunner.
        return services;
    }
}
```

Wire `AddGenerateBindings()` into `build\_build\Program.cs` composition root next to `AddPreFlightCheck()` / `AddHarvest()` / etc.

- [ ] **Step 4: Write the first failing config test**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\Sdl2CoreGenerationConfigTests.cs`:

```csharp
using Build.Targets.GenerateBindings;

namespace Build.Tests.Unit.Targets.GenerateBindings;

public sealed class Sdl2CoreGenerationConfigTests
{
    [Test]
    public async Task Default_Should_Expose_Sdl2_Core_Identity()
    {
        var config = Sdl2CoreGenerationConfig.Default;

        await Assert.That(config.Family).IsEqualTo("sdl2-core");
        await Assert.That(config.LibraryName).IsEqualTo("SDL2");
        await Assert.That(config.Namespace).IsEqualTo("SDL2");
        await Assert.That(config.PrimaryClassName).IsEqualTo("SDL");
        await Assert.That(config.OwnedPrefixes).Contains("SDL_");
    }

    [Test]
    public async Task Default_Should_Mark_SysWMinfo_Typed_Union_As_Deferred_To_Stage_2()
    {
        var config = Sdl2CoreGenerationConfig.Default;

        await Assert.That(config.DeferredDeclarations).Contains("SDL_SysWMinfo");
        await Assert.That(config.DeferredDeclarations).Contains("SDL_SysWMmsg");
    }
}
```

Run:

```pwsh
dotnet test build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter FullyQualifiedName~Sdl2CoreGenerationConfigTests
```

Expected: FAIL because `Sdl2CoreGenerationConfig` does not exist.

- [ ] **Step 5: Add minimal Sdl2CoreGenerationConfig and GenerateBindingsRequest**

Create `build\_build\Targets\GenerateBindings\Sdl2CoreGenerationConfig.cs`:

```csharp
namespace Build.Targets.GenerateBindings;

internal sealed record Sdl2CoreGenerationConfig(
    string Family,
    string LibraryName,
    string Namespace,
    string PrimaryClassName,
    IReadOnlyList<string> OwnedPrefixes,
    IReadOnlyList<string> DeferredDeclarations)
{
    public static Sdl2CoreGenerationConfig Default { get; } = new(
        Family: "sdl2-core",
        LibraryName: "SDL2",
        Namespace: "SDL2",
        PrimaryClassName: "SDL",
        OwnedPrefixes: ["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"],
        DeferredDeclarations:
        [
            // SDL_syswm.h typed-union layout deferred to Stage 2 per revised plan (2026-05-15).
            // Stage 1 emits SDL_GetWindowWMInfo as a function with opaque SDL_SysWMinfo*; the typed
            // SDL_SysWMinfo / SDL_SysWMmsg union + 64-byte layout lock + forward-declaration stub library
            // land in Stage 2. KnownUnsupportedDeclarationPolicy categorizes these as `deferred-to-stage-2`.
            "SDL_SysWMinfo",
            "SDL_SysWMmsg",
        ]);
}
```

Create `build\_build\Targets\GenerateBindings\GenerateBindingsRequest.cs`:

```csharp
namespace Build.Targets.GenerateBindings;

internal sealed record GenerateBindingsRequest(
    Sdl2CoreGenerationConfig Config,
    DirectoryInfo VcpkgInstalledDirectory,
    string Triplet,
    DirectoryInfo OutputDirectory);
```

Re-run the test:

```pwsh
dotnet test build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --filter FullyQualifiedName~Sdl2CoreGenerationConfigTests
```

Expected: PASS.

- [ ] **Step 6: Verify the build host still compiles cleanly**

Run:

```pwsh
dotnet build build\_build\Build.csproj -c Release
dotnet test build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Expected: build succeeds; all existing tests + the new `Sdl2CoreGenerationConfigTests` pass. If `dotnet build` complains about the new CppAst packages on a Windows or macOS dev machine (transitive runtime resolution), document the diagnostic in the task scratchpad — the runtime is intentionally Linux-only, so a managed-only build should still succeed on a Windows host (no native libclang load happens at compile time).

**Commit checkpoint.** Summarize: "Stage 1 Task 1 — fold generator package set into Cake build host; scaffold GenerateBindings target folder + Sdl2CoreGenerationConfig + GenerateBindingsRequest; tests join build/_build.Tests." Present the commit message draft, wait for approval, then commit.

---

## Task 2: Header Set Resolution, Fingerprinting, and Deterministic Stamp Model

**Files:**

- Create: `build\_build\Targets\GenerateBindings\HeaderSet\HeaderSet.cs`
- Create: `build\_build\Targets\GenerateBindings\HeaderSet\HeaderSetResolver.cs`
- Create: `build\_build\Targets\GenerateBindings\HeaderSet\HeaderSetFingerprint.cs`
- Create: `build\_build\Targets\GenerateBindings\Stamps\GeneratedStamp.cs`
- Create: `build\_build\Targets\GenerateBindings\Stamps\GeneratedStampWriter.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\TestWorkspace.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\HeaderSet\HeaderSetResolverTests.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\HeaderSet\HeaderSetFingerprintTests.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Stamps\GeneratedStampWriterTests.cs`

- [ ] **Step 1: Write failing header resolver tests**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\TestWorkspace.cs`:

```csharp
namespace Build.Tests.Unit.Targets.GenerateBindings;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        Root = Directory.CreateTempSubdirectory("janset-sdl2-generator-tests-");
    }

    public DirectoryInfo Root { get; }

    public string WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(Root.FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        Root.Delete(recursive: true);
    }
}
```

Create `build\_build.Tests\Unit\Targets\GenerateBindings\HeaderSet\HeaderSetResolverTests.cs`:

```csharp
using Build.Targets.GenerateBindings.HeaderSet;

namespace Build.Tests.Unit.Targets.GenerateBindings.HeaderSet;

public sealed class HeaderSetResolverTests
{
    [Test]
    public async Task ResolveCoreHeaders_Should_Return_Canonical_Core_Header_Paths()
    {
        using var workspace = new TestWorkspace();
        var includeRoot = Path.Combine(workspace.Root.FullName, "vcpkg_installed", "x64-linux-hybrid", "include", "SDL2");
        Directory.CreateDirectory(includeRoot);

        foreach (var header in HeaderSetResolver.Sdl2CoreHeaders)
        {
            File.WriteAllText(Path.Combine(includeRoot, header), "/* header */");
        }

        var resolver = new HeaderSetResolver();
        var result = resolver.ResolveCoreHeaders(
            new DirectoryInfo(Path.Combine(workspace.Root.FullName, "vcpkg_installed")),
            "x64-linux-hybrid");

        await Assert.That(result.Headers.Select(path => Path.GetFileName(path.FullName)))
            .IsEquivalentTo(HeaderSetResolver.Sdl2CoreHeaders);
    }

    [Test]
    public async Task ResolveCoreHeaders_Should_Throw_When_Any_Core_Header_Is_Missing()
    {
        using var workspace = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace.Root.FullName, "vcpkg_installed", "x64-linux-hybrid", "include", "SDL2"));

        var resolver = new HeaderSetResolver();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            resolver.ResolveCoreHeaders(
                new DirectoryInfo(Path.Combine(workspace.Root.FullName, "vcpkg_installed")),
                "x64-linux-hybrid");
        });

        await Assert.That(exception.Message).Contains("SDL.h");
    }
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because `HeaderSetResolver` does not exist.

- [ ] **Step 2: Implement header set resolver**

Create `build\_build\Targets\GenerateBindings\HeaderSet\HeaderSet.cs`:

```csharp
namespace Build.Targets.GenerateBindings.HeaderSet;

internal sealed record HeaderSet(
    DirectoryInfo IncludeRoot,
    DirectoryInfo Sdl2IncludeDirectory,
    IReadOnlyList<FileInfo> Headers);
```

Create `build\_build\Targets\GenerateBindings\HeaderSet\HeaderSetResolver.cs`:

```csharp
namespace Build.Targets.GenerateBindings.HeaderSet;

internal sealed class HeaderSetResolver
{
    public static readonly string[] Sdl2CoreHeaders =
    [
        "SDL.h",
        "SDL_assert.h",
        "SDL_atomic.h",
        "SDL_audio.h",
        "SDL_bits.h",
        "SDL_blendmode.h",
        "SDL_clipboard.h",
        "SDL_config.h",
        "SDL_cpuinfo.h",
        "SDL_endian.h",
        "SDL_error.h",
        "SDL_events.h",
        "SDL_filesystem.h",
        "SDL_gamecontroller.h",
        "SDL_gesture.h",
        "SDL_guid.h",
        "SDL_haptic.h",
        "SDL_hidapi.h",
        "SDL_hints.h",
        "SDL_joystick.h",
        "SDL_keyboard.h",
        "SDL_keycode.h",
        "SDL_loadso.h",
        "SDL_locale.h",
        "SDL_log.h",
        "SDL_main.h",
        "SDL_messagebox.h",
        "SDL_metal.h",
        "SDL_misc.h",
        "SDL_mouse.h",
        "SDL_mutex.h",
        "SDL_pixels.h",
        "SDL_platform.h",
        "SDL_power.h",
        "SDL_quit.h",
        "SDL_rect.h",
        "SDL_render.h",
        "SDL_revision.h",
        "SDL_rwops.h",
        "SDL_scancode.h",
        "SDL_sensor.h",
        "SDL_shape.h",
        "SDL_stdinc.h",
        "SDL_surface.h",
        "SDL_system.h",
        "SDL_syswm.h",
        "SDL_thread.h",
        "SDL_timer.h",
        "SDL_touch.h",
        "SDL_types.h",
        "SDL_version.h",
        "SDL_video.h",
        "begin_code.h",
        "close_code.h"
    ];

    public HeaderSet ResolveCoreHeaders(DirectoryInfo vcpkgInstalledDirectory, string triplet)
    {
        ArgumentNullException.ThrowIfNull(vcpkgInstalledDirectory);

        if (string.IsNullOrWhiteSpace(triplet))
        {
            throw new ArgumentException("Triplet is required.", nameof(triplet));
        }

        var includeRoot = new DirectoryInfo(Path.Combine(vcpkgInstalledDirectory.FullName, triplet, "include"));
        var sdl2Include = new DirectoryInfo(Path.Combine(includeRoot.FullName, "SDL2"));

        var headers = new List<FileInfo>(Sdl2CoreHeaders.Length);
        foreach (var header in Sdl2CoreHeaders)
        {
            var path = new FileInfo(Path.Combine(sdl2Include.FullName, header));
            if (!path.Exists)
            {
                throw new InvalidOperationException($"Required SDL2.Core header '{header}' was not found at '{path.FullName}'.");
            }

            headers.Add(path);
        }

        return new HeaderSet(includeRoot, sdl2Include, headers);
    }
}
```

- [ ] **Step 3: Write failing fingerprint and stamp tests**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\HeaderSet\HeaderSetFingerprintTests.cs`:

```csharp
using Build.Targets.GenerateBindings.HeaderSet;

namespace Build.Tests.Unit.Targets.GenerateBindings.HeaderSet;

public sealed class HeaderSetFingerprintTests
{
    [Test]
    public async Task Compute_Should_Be_Stable_When_Line_Endings_Differ()
    {
        using var workspace = new TestWorkspace();
        var includeRoot = Directory.CreateDirectory(Path.Combine(workspace.Root.FullName, "include"));
        var sdl2Root = Directory.CreateDirectory(Path.Combine(includeRoot.FullName, "SDL2"));
        var header = new FileInfo(Path.Combine(sdl2Root.FullName, "SDL.h"));

        File.WriteAllText(header.FullName, "line1\r\nline2\r\n");
        var crlf = HeaderSetFingerprint.Compute(new HeaderSet(includeRoot, sdl2Root, [header]));

        File.WriteAllText(header.FullName, "line1\nline2\n");
        var lf = HeaderSetFingerprint.Compute(new HeaderSet(includeRoot, sdl2Root, [header]));

        await Assert.That(lf.Hash).IsEqualTo(crlf.Hash);
        await Assert.That(lf.HeaderCount).IsEqualTo(1);
    }
}
```

Create `build\_build.Tests\Unit\Targets\GenerateBindings\Stamps\GeneratedStampWriterTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Stamps;

namespace Build.Tests.Unit.Targets.GenerateBindings.Stamps;

public sealed class GeneratedStampWriterTests
{
    [Test]
    public async Task Write_Should_Produce_Deterministic_Json_Without_Wall_Clock_Time()
    {
        using var workspace = new TestWorkspace();
        var output = Directory.CreateDirectory(Path.Combine(workspace.Root.FullName, "Generated"));
        var stamp = new GeneratedStamp(
            SchemaVersion: 1,
            Family: "sdl2-core",
            GeneratorAssembly: "Build.Targets.GenerateBindings",
            CppAstVersion: "0.24.0",
            LibClangVersion: "20.1.2",
            VcpkgTriplet: "x64-linux-hybrid",
            VcpkgManifestHash: "sha256:vcpkg",
            VcpkgBaseline: "0b88aacde46a853151730fbe7d0b7ee45f4b6864",
            ManifestLibraryVersion: "2.32.10",
            HeaderFingerprint: "sha256:abc",
            HeaderCount: 54,
            ParseViews: ["Neutral", "Windows"]);

        var writer = new GeneratedStampWriter();
        writer.Write(output, stamp);
        var first = File.ReadAllText(Path.Combine(output.FullName, ".generated-stamp"));
        writer.Write(output, stamp);
        var second = File.ReadAllText(Path.Combine(output.FullName, ".generated-stamp"));

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(second).DoesNotContain("generated_at");
    }
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because fingerprint and stamp types do not exist.

- [ ] **Step 4: Implement fingerprint and stamp writer**

Create `build\_build\Targets\GenerateBindings\HeaderSet\HeaderSetFingerprint.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Build.Targets.GenerateBindings.HeaderSet;

internal sealed record HeaderSetFingerprint(string Hash, int HeaderCount)
{
    public static HeaderSetFingerprint Compute(HeaderSet headerSet)
    {
        ArgumentNullException.ThrowIfNull(headerSet);

        var builder = new StringBuilder();
        foreach (var header in headerSet.Headers.OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(headerSet.IncludeRoot.FullName, header.FullName)
                .Replace('\\', '/');
            builder.Append(relativePath).Append('\n');
            builder.Append(NormalizeLineEndings(File.ReadAllText(header.FullName))).Append('\n');
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
        return new HeaderSetFingerprint($"sha256:{hash}", headerSet.Headers.Count);
    }

    private static string NormalizeLineEndings(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
}
```

Create `build\_build\Targets\GenerateBindings\Stamps\GeneratedStamp.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Stamps;

internal sealed record GeneratedStamp(
    int SchemaVersion,
    string Family,
    string GeneratorAssembly,
    string CppAstVersion,
    string LibClangVersion,
    string VcpkgTriplet,
    string VcpkgManifestHash,
    string VcpkgBaseline,
    string ManifestLibraryVersion,
    string HeaderFingerprint,
    int HeaderCount,
    IReadOnlyList<string> ParseViews);
```

Create `build\_build\Targets\GenerateBindings\Stamps\GeneratedStampWriter.cs`:

```csharp
using System.Text.Json;

namespace Build.Targets.GenerateBindings.Stamps;

internal sealed class GeneratedStampWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public void Write(DirectoryInfo outputDirectory, GeneratedStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(stamp);

        outputDirectory.Create();
        var path = Path.Combine(outputDirectory.FullName, ".generated-stamp");
        var json = JsonSerializer.Serialize(stamp, SerializerOptions) + Environment.NewLine;
        File.WriteAllText(path, json);
    }
}
```

- [ ] **Step 5: Run tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Expected: all generator tests pass.

- [ ] **Step 6: Commit checkpoint**

Proposed commit message:

```text
feat: resolve and stamp SDL2 headers
```

Before committing, present the summary and proposed message to Deniz and wait for approval.

---

## Task 3: Platform Catalog and CppAst Parse Runner

**Files:**

- Create: `build\_build\Targets\GenerateBindings\Parsing\PlatformCondition.cs`
- Create: `build\_build\Targets\GenerateBindings\Parsing\PlatformParseView.cs`
- Create: `build\_build\Targets\GenerateBindings\Parsing\PlatformCatalog.cs`
- Create: `build\_build\Targets\GenerateBindings\Parsing\CppAstParseRunner.cs`
- Create: `build\_build\Targets\GenerateBindings\Parsing\CppAstParseResult.cs`
- Create: `build\_build\Targets\GenerateBindings\Parsing\ParseDiagnosticFormatter.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Parsing\PlatformCatalogTests.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Parsing\CppAstParseRunnerTests.cs`

**No `Stubs/` subdirectory in this task.** The 2026-05-14 draft proposed hand-written platform stubs (windows.h, winrt/Inspectable.h, X11/Xlib.h, etc.). The 2026-05-15 revision retracts those — Stage 1's preprocessor-macro-only strategy parses against the Linux container's apt sysroot directly. See the Scope Decisions section at the top of this plan and the "no platform stubs in Stage 1" step below.

- [ ] **Step 1: Write failing platform catalog tests**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\Parsing\PlatformCatalogTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Parsing;

namespace Build.Tests.Unit.Targets.GenerateBindings.Parsing;

public sealed class PlatformCatalogTests
{
    [Test]
    public async Task CreateSdl2Catalog_Should_Include_Neutral_And_All_Required_Core_Platforms()
    {
        var catalog = PlatformCatalog.CreateSdl2Catalog();
        var names = catalog.ParseViews.Select(view => view.Name).ToArray();

        await Assert.That(names).Contains("Neutral");
        await Assert.That(names).Contains("Windows");
        await Assert.That(names).Contains("WinRT");
        await Assert.That(names).Contains("GDK");
        await Assert.That(names).Contains("Linux");
        await Assert.That(names).Contains("MacOS");
        await Assert.That(names).Contains("IOS");
        await Assert.That(names).Contains("Android");
        await Assert.That(names).Contains("TvOS");
        await Assert.That(names).Contains("X11");
        await Assert.That(names).Contains("Wayland");
        await Assert.That(names).Contains("KmsDrm");
        await Assert.That(names).Contains("Cocoa");
        await Assert.That(names).Contains("UIKit");
        await Assert.That(names).Contains("DirectFB");
        await Assert.That(names).Contains("Vivante");
        await Assert.That(names).Contains("Mir");
        await Assert.That(names).Contains("OS2");
    }

    [Test]
    public async Task CreateSdl2Catalog_Should_Map_Os_Views_To_SupportedOSPlatform_When_Available()
    {
        var catalog = PlatformCatalog.CreateSdl2Catalog();

        await Assert.That(catalog.ParseViews.Single(view => view.Name == "Windows").SupportedOsPlatform).IsEqualTo("windows");
        await Assert.That(catalog.ParseViews.Single(view => view.Name == "Linux").SupportedOsPlatform).IsEqualTo("linux");
        await Assert.That(catalog.ParseViews.Single(view => view.Name == "MacOS").SupportedOsPlatform).IsEqualTo("macos");
        await Assert.That(catalog.ParseViews.Single(view => view.Name == "Android").SupportedOsPlatform).IsEqualTo("android");
        await Assert.That(catalog.ParseViews.Single(view => view.Name == "X11").SupportedOsPlatform).IsNull();
    }
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because parsing catalog types do not exist.

- [ ] **Step 2: Implement platform catalog**

Create `build\_build\Targets\GenerateBindings\Parsing\PlatformCondition.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Parsing;

internal enum PlatformConditionKind
{
    Neutral,
    OperatingSystem,
    Backend
}
```

Create `build\_build\Targets\GenerateBindings\Parsing\PlatformParseView.cs`. **No `TargetSystem` field** — the preprocessor-macro-only strategy does not use libclang `--target` cross-compile flags.

```csharp
namespace Build.Targets.GenerateBindings.Parsing;

internal sealed record PlatformParseView(
    string Name,
    PlatformConditionKind Kind,
    string? SupportedOsPlatform,
    IReadOnlyList<string> Defines,
    IReadOnlyList<string> Undefines);
```

Create `build\_build\Targets\GenerateBindings\Parsing\PlatformCatalog.cs`. The catalog is a small `(OsCondition, BackendCondition[])` tuple set — **one entry per parse view**. Stage 1's SDL2.Core catalog has 8 entries (Neutral + 7 OS passes). Backends that compile-in on each OS are bundled into that OS's pass (e.g., Linux pass enables X11 + Wayland + KMSDRM together). Niche backends (DirectFB, Vivante, MIR, OS/2) are documented Stage 1 exclusions per the revised plan header. Per-backend attribution is a Stage 2 concern that lands with `SDL_syswm.h` typed-union work.

```csharp
namespace Build.Targets.GenerateBindings.Parsing;

internal sealed record PlatformCatalog(IReadOnlyList<PlatformParseView> ParseViews)
{
    // Master "undefine-all-platform-macros" hygiene list. The number of entries here is hygiene only;
    // the number of PASSES equals the count of ParseViews below. Each pass nukes this entire list, then
    // defines the macro group for its specific (OS, backend[]) tuple. ppy/SDL3-CS pattern.
    public static readonly IReadOnlyList<string> AllPlatformMacros =
    [
        "_WIN32", "WIN32", "__WIN32__", "__WINDOWS__", "__WINRT__", "__GDK__", "__WINGDK__",
        "linux", "__linux", "__linux__", "__LINUX__", "__APPLE__", "__MACOSX__", "__IPHONEOS__",
        "__ANDROID__", "__TVOS__",
        "SDL_VIDEO_DRIVER_WINDOWS", "SDL_VIDEO_DRIVER_WINRT",
        "SDL_VIDEO_DRIVER_X11", "SDL_VIDEO_DRIVER_WAYLAND", "SDL_VIDEO_DRIVER_KMSDRM",
        "SDL_VIDEO_DRIVER_COCOA", "SDL_VIDEO_DRIVER_UIKIT", "SDL_VIDEO_DRIVER_DIRECTFB",
        "SDL_VIDEO_DRIVER_VIVANTE", "SDL_VIDEO_DRIVER_ANDROID", "SDL_VIDEO_DRIVER_MIR",
        "SDL_VIDEO_DRIVER_OS2"
    ];

    public static PlatformCatalog CreateSdl2Catalog()
    {
        PlatformParseView View(
            string name,
            PlatformConditionKind kind,
            string? supportedOsPlatform,
            string[] defines)
        {
            var definedNames = defines
                .Select(value => value.Split('=', 2)[0])
                .ToHashSet(StringComparer.Ordinal);

            var undefines = AllPlatformMacros
                .Where(macro => !definedNames.Contains(macro))
                .ToArray();

            return new PlatformParseView(name, kind, supportedOsPlatform, defines, undefines);
        }

        return new PlatformCatalog([
            View("Neutral",        PlatformConditionKind.Neutral,         null,      []),
            View("WindowsDesktop", PlatformConditionKind.OperatingSystem, "windows", ["_WIN32=1", "WIN32=1", "__WIN32__=1", "__WINDOWS__=1", "SDL_VIDEO_DRIVER_WINDOWS=1"]),
            View("WinRT",          PlatformConditionKind.OperatingSystem, "windows", ["_WIN32=1", "__WINRT__=1", "SDL_VIDEO_DRIVER_WINRT=1"]),
            View("GDK",            PlatformConditionKind.OperatingSystem, "windows", ["_WIN32=1", "__GDK__=1", "__WINGDK__=1", "SDL_VIDEO_DRIVER_WINDOWS=1"]),
            View("Linux",          PlatformConditionKind.OperatingSystem, "linux",   ["linux=1", "__linux=1", "__linux__=1", "__LINUX__=1", "SDL_VIDEO_DRIVER_X11=1", "SDL_VIDEO_DRIVER_WAYLAND=1", "SDL_VIDEO_DRIVER_KMSDRM=1"]),
            View("MacOS",          PlatformConditionKind.OperatingSystem, "osx",     ["__APPLE__=1", "__MACOSX__=1", "SDL_VIDEO_DRIVER_COCOA=1"]),
            View("IOS",            PlatformConditionKind.OperatingSystem, "ios",     ["__APPLE__=1", "__IPHONEOS__=1", "SDL_VIDEO_DRIVER_UIKIT=1"]),
            View("Android",        PlatformConditionKind.OperatingSystem, "android", ["__ANDROID__=1", "SDL_VIDEO_DRIVER_ANDROID=1"]),
        ]);
    }
}
```

Note: `PlatformParseView` drops the per-view `TargetSystem` field — Stage 1's preprocessor-macro-only strategy does not use `--target` cross-compile flags. If you saw `TargetSystem` in the earlier draft of `PlatformParseView.cs`, remove it.

- [ ] **Step 3: No platform stub headers in Stage 1**

The 2026-05-14 draft of this plan proposed a `Stubs/` subdirectory with hand-written `windows.h`, `winrt/Inspectable.h`, `X11/Xlib.h`, `X11/Xatom.h`, `directfb.h`, `os2.h`, `wayland-client.h` shims. **That has been retracted on 2026-05-15.**

The Linux container's apt sysroot already provides the dev headers SDL needs for X11 / Wayland / EGL / etc. parses. SDL's own public headers carry the cross-platform opaque-type forward declarations (`typedef struct _NSWindow NSWindow;` at `SDL_syswm.h:86`, `typedef struct _UIWindow UIWindow;` at `:94`, `typedef struct ANativeWindow ANativeWindow;` at `:105`, `struct gbm_device;` at `:120`) so the parser does not need platform SDK headers to resolve function signatures across `SDL_VIDEO_DRIVER_*` branches. Verified against the local CppAst spike at `tools/binding-spike/cppast/generator/Program.cs:42-72` and ppy/SDL3-CS Dockerfile + `generate_bindings.py` (WebFetch 2026-05-15) — neither uses mingw-w64, neither ships platform stubs, both rely on preprocessor-macro switching alone.

Stage 2 introduces a small forward-declaration stub library (~15–20 types) **specifically** for the `SDL_SysWMinfo`/`SDL_SysWMmsg` typed-union layout work — that is the only place where the 64-byte union requires the parser to know about `HWND`/`IInspectable`/etc. as named opaque types. Stage 1 emits `SDL_GetWindowWMInfo` as a function with opaque `SDL_SysWMinfo*` parameter and records the typed-union deferral; no Stage 1 stubs are needed for that.

If a future SDL header bump introduces a public surface that genuinely needs a stub, classify it in `KnownUnsupportedDeclarationPolicy` first and stop for design review rather than reintroducing a `Stubs/` folder ad-hoc.

- [ ] **Step 4: Write failing parse runner test**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\Parsing\CppAstParseRunnerTests.cs`:

```csharp
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Parsing;

namespace Build.Tests.Unit.Targets.GenerateBindings.Parsing;

public sealed class CppAstParseRunnerTests
{
    [Test]
    public async Task Parse_Should_Return_Compilation_When_Header_Is_Valid()
    {
        using var workspace = new TestWorkspace();
        var includeRoot = Directory.CreateDirectory(Path.Combine(workspace.Root.FullName, "include"));
        var sdl2Root = Directory.CreateDirectory(Path.Combine(includeRoot.FullName, "SDL2"));
        var header = new FileInfo(Path.Combine(sdl2Root.FullName, "SDL_test.h"));
        File.WriteAllText(header.FullName, "typedef int SDL_bool;\nextern int SDL_Init(unsigned int flags);\n");

        var parseView = new PlatformParseView("Neutral", PlatformConditionKind.Neutral, null, [], []);
        var runner = new CppAstParseRunner(new ParseDiagnosticFormatter());

        var result = runner.Parse(new HeaderSet(includeRoot, sdl2Root, [header]), parseView);

        await Assert.That(result.Compilation.HasErrors).IsFalse();
        await Assert.That(result.ParseView.Name).IsEqualTo("Neutral");
    }
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because parse runner types do not exist.

- [ ] **Step 5: Implement CppAst parse runner**

Create `build\_build\Targets\GenerateBindings\Parsing\CppAstParseResult.cs`:

```csharp
using CppAst;

namespace Build.Targets.GenerateBindings.Parsing;

internal sealed record CppAstParseResult(
    PlatformParseView ParseView,
    CppCompilation Compilation);
```

Create `build\_build\Targets\GenerateBindings\Parsing\ParseDiagnosticFormatter.cs`:

```csharp
using CppAst;

namespace Build.Targets.GenerateBindings.Parsing;

internal sealed class ParseDiagnosticFormatter
{
    public string FormatErrors(string parseViewName, CppCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var messages = compilation.Diagnostics.Messages
            .Where(message => message.Type == CppLogMessageType.Error)
            .Select(message => "  " + message)
            .ToArray();

        return messages.Length == 0
            ? $"CppAst parse failed for {parseViewName} without diagnostics."
            : $"CppAst parse failed for {parseViewName}:{Environment.NewLine}{string.Join(Environment.NewLine, messages)}";
    }
}
```

Create `build\_build\Targets\GenerateBindings\Parsing\CppAstParseRunner.cs`:

```csharp
using CppAst;
using Build.Targets.GenerateBindings.HeaderSet;

namespace Build.Targets.GenerateBindings.Parsing;

internal sealed class CppAstParseRunner(ParseDiagnosticFormatter diagnosticFormatter)
{
    private readonly ParseDiagnosticFormatter _diagnosticFormatter = diagnosticFormatter ?? throw new ArgumentNullException(nameof(diagnosticFormatter));

    public CppAstParseResult Parse(HeaderSet headerSet, PlatformParseView parseView)
    {
        ArgumentNullException.ThrowIfNull(headerSet);
        ArgumentNullException.ThrowIfNull(parseView);

        // Stage 1 runs inside the linux-builder container. apt sysroot resolves <stdint.h>, <X11/Xlib.h>,
        // <wayland-client.h>, etc. transparently — no stub include folders are needed. The Linux-canonical
        // host guard in GenerateBindingsTask ensures this code never runs on a non-Linux host where the
        // apt sysroot would be unavailable.
        //
        // Preprocessor-macro switching only — no --target / TargetSystem. SDL's own forward declarations
        // (typedef struct _NSWindow NSWindow; in SDL_syswm.h:86 etc.) carry the cross-platform opaque types.
        var options = new CppParserOptions
        {
            ParseMacros = true,
            SystemIncludeFolders = { headerSet.IncludeRoot.FullName },
            Defines = { "SDL_DECLSPEC=", "__PRFCHWINTRIN_H=1" }
        };

        foreach (var define in parseView.Defines)
        {
            options.Defines.Add(define);
        }

        foreach (var undefine in parseView.Undefines)
        {
            options.AdditionalArguments.Add($"-U{undefine}");
        }

        var compilation = CppParser.ParseFiles(headerSet.Headers.Select(file => file.FullName).ToList(), options);
        if (compilation.HasErrors)
        {
            throw new InvalidOperationException(_diagnosticFormatter.FormatErrors(parseView.Name, compilation));
        }

        return new CppAstParseResult(parseView, compilation);
    }
}
```

**No `Stubs\**\*` content include in `build/_build/Build.csproj`.** The 2026-05-14 draft included a `<Content Include="Stubs\**\*">` block to copy hand-written platform stubs to the generator output. Stage 1's preprocessor-macro-only strategy does not use stubs, so this csproj content include must not be added. The Linux container's apt sysroot provides every system header CppAst needs during the parse views.

- [ ] **Step 6: Run tests and real header parse smoke**

Run inside the Linux container (via `tools.cs generate-bindings` from a Windows/macOS dev host, or directly with `dotnet run` from a Linux dev host):

```pwsh
dotnet test build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
dotnet run --project build\_build\Build.csproj -- --target GenerateBindings --family sdl2-core
```

Expected: tests pass. The Cake target's Linux-canonical guard will refuse to run on a Windows or macOS dev host; use `tools.cs generate-bindings --family sdl2-core` from those hosts. Tests themselves run fine on any host (they don't load the native libclang runtime — they exercise the model and emitter logic only).

- [ ] **Step 7: Commit checkpoint**

Proposed commit message:

```text
feat: add SDL2 platform parse catalog
```

Before committing, present the summary and proposed message to Deniz and wait for approval.

---

## Task 4: Binding Model, Merge Policy, and Fail-Closed Validators

**Files:**

- Create: `build\_build\Targets\GenerateBindings\Model\BindingTypeRef.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\BindingParameter.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\BindingDeclaration.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\BindingModel.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\CoreOwnedTypeMap.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\TypeMappingPolicy.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\KnownUnsupportedDeclarationPolicy.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\DeclarationCollector.cs`
- Create: `build\_build\Targets\GenerateBindings\Model\DeclarationMergePolicy.cs`
- Create: `build\_build\Targets\GenerateBindings\Validation\*.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Model\*.cs`

- [ ] **Step 1: Write failing type mapping tests**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\Model\TypeMappingPolicyTests.cs`:

```csharp
using CppAst;
using Build.Targets.GenerateBindings.Model;

namespace Build.Tests.Unit.Targets.GenerateBindings.Model;

public sealed class TypeMappingPolicyTests
{
    [Test]
    public async Task MapTypedefName_Should_Map_SdlBool_To_Int()
    {
        var policy = TypeMappingPolicy.CreateSdl2Core();

        var result = policy.MapKnownTypedef("SDL_bool");

        await Assert.That(result.ManagedName).IsEqualTo("int");
        await Assert.That(result.NativeName).IsEqualTo("SDL_bool");
    }

    [Test]
    public async Task MapTypedefName_Should_Map_Opaque_Window_To_Typed_Handle()
    {
        var policy = TypeMappingPolicy.CreateSdl2Core();

        var result = policy.MapKnownTypedefPointer("SDL_Window");

        await Assert.That(result.ManagedName).IsEqualTo("SDL_Window");
        await Assert.That(result.NativeName).IsEqualTo("SDL_Window*");
        await Assert.That(result.IsHandle).IsTrue();
    }

    [Test]
    public async Task KnownUnsupportedDeclarationPolicy_Should_Classify_Sdl_Log_Variadic_Functions()
    {
        var policy = KnownUnsupportedDeclarationPolicy.CreateSdl2Core();

        var reason = policy.TryGetReason("SDL_Log");

        await Assert.That(reason).Contains("C variadic");
    }
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because model types do not exist.

- [ ] **Step 2: Implement core type and unsupported declaration policies**

Create `build\_build\Targets\GenerateBindings\Model\BindingTypeRef.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed record BindingTypeRef(
    string ManagedName,
    string NativeName,
    bool IsPointer,
    bool IsHandle,
    bool IsStringLike);
```

Create `build\_build\Targets\GenerateBindings\Model\TypeMappingPolicy.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed class TypeMappingPolicy
{
    private readonly Dictionary<string, BindingTypeRef> _typedefs;
    private readonly HashSet<string> _opaqueHandles;

    private TypeMappingPolicy(Dictionary<string, BindingTypeRef> typedefs, HashSet<string> opaqueHandles)
    {
        _typedefs = typedefs;
        _opaqueHandles = opaqueHandles;
    }

    public static TypeMappingPolicy CreateSdl2Core()
    {
        return new TypeMappingPolicy(
            new Dictionary<string, BindingTypeRef>(StringComparer.Ordinal)
            {
                ["SDL_bool"] = new("int", "SDL_bool", IsPointer: false, IsHandle: false, IsStringLike: false),
                ["Uint8"] = new("byte", "Uint8", IsPointer: false, IsHandle: false, IsStringLike: false),
                ["Sint8"] = new("sbyte", "Sint8", IsPointer: false, IsHandle: false, IsStringLike: false),
                ["Uint16"] = new("ushort", "Uint16", IsPointer: false, IsHandle: false, IsStringLike: false),
                ["Sint16"] = new("short", "Sint16", IsPointer: false, IsHandle: false, IsStringLike: false),
                ["Uint32"] = new("uint", "Uint32", IsPointer: false, IsHandle: false, IsStringLike: false),
                ["Sint32"] = new("int", "Sint32", IsPointer: false, IsHandle: false, IsStringLike: false),
                ["Uint64"] = new("ulong", "Uint64", IsPointer: false, IsHandle: false, IsStringLike: false),
                ["Sint64"] = new("long", "Sint64", IsPointer: false, IsHandle: false, IsStringLike: false),
                ["size_t"] = new("nuint", "size_t", IsPointer: false, IsHandle: false, IsStringLike: false)
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                "SDL_Window",
                "SDL_Renderer",
                "SDL_Texture",
                "SDL_Surface",
                "SDL_RWops",
                "SDL_Cursor",
                "SDL_Joystick",
                "SDL_GameController",
                "SDL_Haptic",
                "SDL_Sensor",
                "SDL_Thread",
                "SDL_mutex",
                "SDL_sem",
                "SDL_cond"
            });
    }

    public BindingTypeRef MapKnownTypedef(string typedefName)
    {
        if (_typedefs.TryGetValue(typedefName, out var mapped))
        {
            return mapped;
        }

        throw new InvalidOperationException($"No SDL2.Core typedef mapping exists for '{typedefName}'.");
    }

    public BindingTypeRef MapKnownTypedefPointer(string typedefName)
    {
        if (_opaqueHandles.Contains(typedefName))
        {
            return new BindingTypeRef(typedefName, typedefName + "*", IsPointer: false, IsHandle: true, IsStringLike: false);
        }

        var mapped = MapKnownTypedef(typedefName);
        return mapped with { NativeName = typedefName + "*", IsPointer = true };
    }
}
```

Create `build\_build\Targets\GenerateBindings\Model\KnownUnsupportedDeclarationPolicy.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed class KnownUnsupportedDeclarationPolicy
{
    private readonly Dictionary<string, string> _reasons;

    private KnownUnsupportedDeclarationPolicy(Dictionary<string, string> reasons)
    {
        _reasons = reasons;
    }

    public static KnownUnsupportedDeclarationPolicy CreateSdl2Core()
    {
        const string variadicReason = "C variadic function. Stage 1 does not emit success-shaped P/Invoke for native varargs.";
        return new KnownUnsupportedDeclarationPolicy(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SDL_Log"] = variadicReason,
            ["SDL_LogVerbose"] = variadicReason,
            ["SDL_LogDebug"] = variadicReason,
            ["SDL_LogInfo"] = variadicReason,
            ["SDL_LogWarn"] = variadicReason,
            ["SDL_LogError"] = variadicReason,
            ["SDL_LogCritical"] = variadicReason,
            ["SDL_LogMessage"] = variadicReason,
            ["SDL_SetError"] = variadicReason,
            ["SDL_InvalidParamError"] = variadicReason,
            ["SDL_sscanf"] = variadicReason,
            ["SDL_snprintf"] = variadicReason
        });
    }

    public string? TryGetReason(string declarationName)
    {
        return _reasons.TryGetValue(declarationName, out var reason) ? reason : null;
    }
}
```

- [ ] **Step 3: Write failing merge-policy tests**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\Model\DeclarationMergePolicyTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;

namespace Build.Tests.Unit.Targets.GenerateBindings.Model;

public sealed class DeclarationMergePolicyTests
{
    [Test]
    public async Task Merge_Should_Keep_Neutral_Declaration_And_Exclude_Matching_Platform_Declaration()
    {
        var neutral = BindingDeclaration.Function(
            Name: "SDL_Init",
            ReturnType: new BindingTypeRef("int", "int", false, false, false),
            Parameters: [new BindingParameter("flags", new BindingTypeRef("uint", "Uint32", false, false, false))],
            SourceHeader: "SDL.h",
            ParseView: "Neutral",
            SupportedOsPlatform: null);

        var windows = neutral with { ParseView = "Windows", SupportedOsPlatform = "windows" };
        var policy = new DeclarationMergePolicy();

        var model = policy.Merge([neutral], [windows]);

        await Assert.That(model.Functions).HasCount().EqualTo(1);
        await Assert.That(model.Functions[0].ParseView).IsEqualTo("Neutral");
    }

    [Test]
    public async Task Merge_Should_Throw_When_Same_Function_Has_Incompatible_Signature()
    {
        var first = BindingDeclaration.Function(
            Name: "SDL_PlatformOnly",
            ReturnType: new BindingTypeRef("int", "int", false, false, false),
            Parameters: [],
            SourceHeader: "SDL_system.h",
            ParseView: "Windows",
            SupportedOsPlatform: "windows");

        var second = first with
        {
            ReturnType = new BindingTypeRef("uint", "Uint32", false, false, false),
            ParseView = "Linux",
            SupportedOsPlatform = "linux"
        };

        var policy = new DeclarationMergePolicy();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => policy.Merge([], [first, second]));

        await Assert.That(exception.Message).Contains("incompatible signatures");
    }
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because declaration model and merge policy do not exist.

- [ ] **Step 4: Implement declaration model and merge policy**

Create `build\_build\Targets\GenerateBindings\Model\BindingParameter.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed record BindingParameter(string Name, BindingTypeRef Type);
```

Create `build\_build\Targets\GenerateBindings\Model\BindingDeclaration.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal enum BindingDeclarationKind
{
    Function,
    Enum,
    Struct,
    Handle,
    Callback,
    Constant
}

internal sealed record BindingDeclaration(
    BindingDeclarationKind Kind,
    string Name,
    BindingTypeRef ReturnType,
    IReadOnlyList<BindingParameter> Parameters,
    string SourceHeader,
    string ParseView,
    string? SupportedOsPlatform)
{
    public static BindingDeclaration Function(
        string Name,
        BindingTypeRef ReturnType,
        IReadOnlyList<BindingParameter> Parameters,
        string SourceHeader,
        string ParseView,
        string? SupportedOsPlatform) =>
        new(BindingDeclarationKind.Function, Name, ReturnType, Parameters, SourceHeader, ParseView, SupportedOsPlatform);

    public string SignatureKey =>
        string.Join("|", ReturnType.ManagedName, string.Join(",", Parameters.Select(parameter => parameter.Type.ManagedName)));
}
```

Create `build\_build\Targets\GenerateBindings\Model\BindingModel.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed record BindingModel(IReadOnlyList<BindingDeclaration> Functions)
{
    public static BindingModel Empty { get; } = new([]);
}
```

Create `build\_build\Targets\GenerateBindings\Model\DeclarationMergePolicy.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed class DeclarationMergePolicy
{
    public BindingModel Merge(
        IReadOnlyList<BindingDeclaration> neutralDeclarations,
        IReadOnlyList<BindingDeclaration> platformDeclarations)
    {
        ArgumentNullException.ThrowIfNull(neutralDeclarations);
        ArgumentNullException.ThrowIfNull(platformDeclarations);

        var neutralByName = neutralDeclarations.ToDictionary(declaration => declaration.Name, StringComparer.Ordinal);
        var merged = new List<BindingDeclaration>(neutralDeclarations);

        foreach (var group in platformDeclarations
                     .Where(declaration => !neutralByName.ContainsKey(declaration.Name))
                     .GroupBy(declaration => declaration.Name, StringComparer.Ordinal))
        {
            var signatures = group.Select(declaration => declaration.SignatureKey).Distinct(StringComparer.Ordinal).ToArray();
            if (signatures.Length > 1)
            {
                throw new InvalidOperationException($"Platform declaration '{group.Key}' has incompatible signatures across parse views.");
            }

            merged.AddRange(group.OrderBy(declaration => declaration.ParseView, StringComparer.Ordinal));
        }

        return new BindingModel(merged
            .OrderBy(declaration => declaration.SourceHeader, StringComparer.Ordinal)
            .ThenBy(declaration => declaration.Name, StringComparer.Ordinal)
            .ThenBy(declaration => declaration.ParseView, StringComparer.Ordinal)
            .ToArray());
    }
}
```

- [ ] **Step 5: Run model tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Expected: all current generator tests pass.

- [ ] **Step 6: Commit checkpoint**

Proposed commit message:

```text
feat: model SDL2 binding declarations
```

Before committing, present the summary and proposed message to Deniz and wait for approval.

---

## Task 5: C# Emitters for Handles, Structs, Constants, Enums, Callbacks, and Commands

**Files:**

- Create: `build\_build\Targets\GenerateBindings\Emitting\CodeWriter.cs`
- Create: `build\_build\Targets\GenerateBindings\Emitting\GeneratedFileSet.cs`
- Create: `build\_build\Targets\GenerateBindings\Emitting\CsCodeGenerator.cs`
- Create: `build\_build\Targets\GenerateBindings\Emitting\CsHandleEmitter.cs`
- Create: `build\_build\Targets\GenerateBindings\Emitting\CsCommandEmitter.cs`
- Create: `build\_build\Targets\GenerateBindings\Emitting\CsStructEmitter.cs`
- Create: `build\_build\Targets\GenerateBindings\Emitting\CsEnumEmitter.cs`
- Create: `build\_build\Targets\GenerateBindings\Emitting\CsConstantEmitter.cs`
- Create: `build\_build\Targets\GenerateBindings\Emitting\CsCallbackEmitter.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\CsCodeGeneratorTests.cs`

- [ ] **Step 1: Write failing emitter tests**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\Emitting\CsCodeGeneratorTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.Model;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emitting;

public sealed class CsCodeGeneratorTests
{
    [Test]
    public async Task Generate_Should_Emit_Typed_Handle_With_Nint_Value_And_Null_Helper()
    {
        var generator = CsCodeGenerator.CreateSdl2Core();
        var output = generator.Generate(BindingModel.Empty with
        {
            Functions = []
        });

        await Assert.That(output.Files["Handles.g.cs"]).Contains("public readonly partial struct SDL_Window");
        await Assert.That(output.Files["Handles.g.cs"]).Contains("private readonly nint _value;");
        await Assert.That(output.Files["Handles.g.cs"]).Contains("public bool IsNull => _value == 0;");
        await Assert.That(output.Files["Handles.g.cs"]).Contains("public static SDL_Window Null => new(0);");
    }

    [Test]
    public async Task Generate_Should_Emit_LibraryImport_And_DllImport_Branches_For_Function()
    {
        var model = new BindingModel([
            BindingDeclaration.Function(
                Name: "SDL_Init",
                ReturnType: new BindingTypeRef("int", "int", false, false, false),
                Parameters: [new BindingParameter("flags", new BindingTypeRef("uint", "Uint32", false, false, false))],
                SourceHeader: "SDL.h",
                ParseView: "Neutral",
                SupportedOsPlatform: null)
        ]);

        var output = CsCodeGenerator.CreateSdl2Core().Generate(model);

        await Assert.That(output.Files["Commands.g.cs"]).Contains("#if NET7_0_OR_GREATER");
        await Assert.That(output.Files["Commands.g.cs"]).Contains("[LibraryImport(LibName, EntryPoint = \"SDL_Init\")");
        await Assert.That(output.Files["Commands.g.cs"]).Contains("[DllImport(LibName, EntryPoint = \"SDL_Init\"");
        await Assert.That(output.Files["Commands.g.cs"]).Contains("public static partial int SDL_Init(uint flags);");
    }

    [Test]
    public async Task Generate_Should_Emit_SupportedOSPlatform_For_Platform_Function()
    {
        var model = new BindingModel([
            BindingDeclaration.Function(
                Name: "SDL_Direct3D9GetAdapterIndex",
                ReturnType: new BindingTypeRef("int", "int", false, false, false),
                Parameters: [new BindingParameter("displayIndex", new BindingTypeRef("int", "int", false, false, false))],
                SourceHeader: "SDL_system.h",
                ParseView: "Windows",
                SupportedOsPlatform: "windows")
        ]);

        var output = CsCodeGenerator.CreateSdl2Core().Generate(model);

        await Assert.That(output.Files["Platform/Windows/Commands.g.cs"]).Contains("[SupportedOSPlatform(\"windows\")]");
    }
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because emitters do not exist.

- [ ] **Step 2: Implement CodeWriter and GeneratedFileSet**

Create `build\_build\Targets\GenerateBindings\Emitting\CodeWriter.cs`:

```csharp
using System.Text;

namespace Build.Targets.GenerateBindings.Emitting;

internal sealed class CodeWriter
{
    private readonly StringBuilder _builder = new();
    private int _indent;

    public void Indent() => _indent++;

    public void Dedent()
    {
        if (_indent == 0)
        {
            throw new InvalidOperationException("Cannot reduce indentation below zero.");
        }

        _indent--;
    }

    public void WriteLine(string line = "")
    {
        if (line.Length > 0)
        {
            _builder.Append(' ', _indent * 4);
        }

        _builder.AppendLine(line);
    }

    public override string ToString() => _builder.ToString();
}
```

Create `build\_build\Targets\GenerateBindings\Emitting\GeneratedFileSet.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Emitting;

internal sealed record GeneratedFileSet(IReadOnlyDictionary<string, string> Files);
```

- [ ] **Step 3: Implement handle and command emitters**

Create `build\_build\Targets\GenerateBindings\Emitting\CsHandleEmitter.cs`:

```csharp
namespace Build.Targets.GenerateBindings.Emitting;

internal sealed class CsHandleEmitter
{
    private static readonly string[] Handles =
    [
        "SDL_Window",
        "SDL_Renderer",
        "SDL_Texture",
        "SDL_Surface",
        "SDL_RWops",
        "SDL_Cursor",
        "SDL_Joystick",
        "SDL_GameController",
        "SDL_Haptic",
        "SDL_Sensor",
        "SDL_Thread",
        "SDL_mutex",
        "SDL_sem",
        "SDL_cond"
    ];

    public string Emit()
    {
        var writer = new CodeWriter();
        writer.WriteLine("// <auto-generated />");
        writer.WriteLine("using System.Diagnostics;");
        writer.WriteLine();
        writer.WriteLine("namespace SDL2;");
        writer.WriteLine();
        writer.WriteLine("public static unsafe partial class SDL");
        writer.WriteLine("{");
        writer.Indent();

        foreach (var handle in Handles)
        {
            writer.WriteLine("[DebuggerDisplay(\"{DebuggerDisplay,nq}\")]");
            writer.WriteLine($"public readonly partial struct {handle}(nint value) : IEquatable<{handle}>");
            writer.WriteLine("{");
            writer.Indent();
            writer.WriteLine("private readonly nint _value = value;");
            writer.WriteLine("public bool IsNull => _value == 0;");
            writer.WriteLine($"public static {handle} Null => new(0);");
            writer.WriteLine("private string DebuggerDisplay => IsNull ? \"null\" : $\"0x{_value:x}\";");
            writer.WriteLine($"public bool Equals({handle} other) => _value == other._value;");
            writer.WriteLine("public override bool Equals(object? obj) => obj is " + handle + " other && Equals(other);");
            writer.WriteLine("public override int GetHashCode() => _value.GetHashCode();");
            writer.WriteLine($"public static bool operator ==({handle} left, {handle} right) => left.Equals(right);");
            writer.WriteLine($"public static bool operator !=({handle} left, {handle} right) => !left.Equals(right);");
            writer.WriteLine($"public static implicit operator nint({handle} handle) => handle._value;");
            writer.WriteLine($"public static implicit operator {handle}(nint value) => new(value);");
            writer.Dedent();
            writer.WriteLine("}");
            writer.WriteLine();
        }

        writer.Dedent();
        writer.WriteLine("}");
        return writer.ToString();
    }
}
```

Create `build\_build\Targets\GenerateBindings\Emitting\CsCommandEmitter.cs`:

```csharp
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emitting;

internal sealed class CsCommandEmitter
{
    public string Emit(IReadOnlyList<BindingDeclaration> functions, string? supportedOsPlatform = null)
    {
        var writer = new CodeWriter();
        writer.WriteLine("// <auto-generated />");
        writer.WriteLine("using System.Runtime.InteropServices;");
        writer.WriteLine("using System.Runtime.CompilerServices;");
        if (supportedOsPlatform is not null)
        {
            writer.WriteLine("using System.Runtime.Versioning;");
        }

        writer.WriteLine();
        writer.WriteLine("namespace SDL2;");
        writer.WriteLine();
        writer.WriteLine("public static unsafe partial class SDL");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine("private const string LibName = \"SDL2\";");

        foreach (var function in functions.OrderBy(function => function.Name, StringComparer.Ordinal))
        {
            writer.WriteLine();
            if (supportedOsPlatform is not null)
            {
                writer.WriteLine($"[SupportedOSPlatform(\"{supportedOsPlatform}\")]");
            }

            var parameters = string.Join(", ", function.Parameters.Select(parameter => $"{parameter.Type.ManagedName} {parameter.Name}"));
            writer.WriteLine("#if NET7_0_OR_GREATER");
            writer.WriteLine($"[LibraryImport(LibName, EntryPoint = \"{function.Name}\")]");
            writer.WriteLine("[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]");
            writer.WriteLine($"public static partial {function.ReturnType.ManagedName} {function.Name}({parameters});");
            writer.WriteLine("#else");
            writer.WriteLine($"[DllImport(LibName, EntryPoint = \"{function.Name}\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]");
            writer.WriteLine($"public static extern {function.ReturnType.ManagedName} {function.Name}({parameters});");
            writer.WriteLine("#endif");
        }

        writer.Dedent();
        writer.WriteLine("}");
        return writer.ToString();
    }
}
```

- [ ] **Step 4: Implement generator coordinator**

Create `build\_build\Targets\GenerateBindings\Emitting\CsCodeGenerator.cs`:

```csharp
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emitting;

internal sealed class CsCodeGenerator(CsHandleEmitter handleEmitter, CsCommandEmitter commandEmitter)
{
    public static CsCodeGenerator CreateSdl2Core() =>
        new(new CsHandleEmitter(), new CsCommandEmitter());

    public GeneratedFileSet Generate(BindingModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var commonFunctions = model.Functions
            .Where(function => function.SupportedOsPlatform is null)
            .ToArray();

        var files = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["Handles.g.cs"] = handleEmitter.Emit(),
            ["Commands.g.cs"] = commandEmitter.Emit(commonFunctions),
            ["Constants.g.cs"] = EmptyCategoryFile("Constants"),
            ["Enums.g.cs"] = EmptyCategoryFile("Enums"),
            ["Structs.g.cs"] = EmptyCategoryFile("Structs"),
            ["Callbacks.g.cs"] = EmptyCategoryFile("Callbacks"),
            ["Marshalling.g.cs"] = EmitMarshalling()
        };

        foreach (var group in model.Functions
                     .Where(function => function.SupportedOsPlatform is not null)
                     .GroupBy(function => function.SupportedOsPlatform!, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var directory = group.Key switch
            {
                "windows" => "Windows",
                "linux" => "Linux",
                "macos" => "MacOS",
                "ios" => "IOS",
                "android" => "Android",
                "tvos" => "TvOS",
                _ => group.Key
            };
            files[$"Platform/{directory}/Commands.g.cs"] = commandEmitter.Emit(group.ToArray(), group.Key);
        }

        return new GeneratedFileSet(files);
    }

    internal static string EmptyCategoryFile(string name)
    {
        return $$"""
        // <auto-generated />
        namespace SDL2;

        public static unsafe partial class SDL
        {
        }

        """;
    }

    private static string EmitMarshalling()
    {
        return """
        // <auto-generated />
        #if NET6_0_OR_GREATER
        using System.Diagnostics.CodeAnalysis;
        #endif
        using System.Runtime.InteropServices;
        using System.Text;

        namespace SDL2;

        public static unsafe partial class SDL
        {
        #if NET6_0_OR_GREATER
            internal static T PtrToStructure<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(nint ptr)
                where T : struct
            {
                return Marshal.PtrToStructure<T>(ptr);
            }

            internal static T GetDelegateForFunctionPointer<T>(nint ptr)
                where T : Delegate
            {
                return Marshal.GetDelegateForFunctionPointer<T>(ptr);
            }
        #else
            internal static T PtrToStructure<T>(nint ptr)
            {
                return (T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T));
            }

            internal static Delegate GetDelegateForFunctionPointer<T>(nint ptr)
            {
                return Marshal.GetDelegateForFunctionPointer((IntPtr)ptr, typeof(T));
            }
        #endif

            internal static int SizeOf<T>()
            {
        #if NETSTANDARD2_0_OR_GREATER || NET6_0_OR_GREATER
                return Marshal.SizeOf<T>();
        #else
                return Marshal.SizeOf(typeof(T));
        #endif
            }

            internal static int Utf8Size(string? value)
            {
                return value is null ? 0 : (value.Length * 4) + 1;
            }

            internal static byte* Utf8Encode(string? value, byte* buffer, int bufferSize)
            {
                if (value is null)
                {
                    return null;
                }

                fixed (char* valuePtr = value)
                {
                    Encoding.UTF8.GetBytes(valuePtr, value.Length + 1, buffer, bufferSize);
                }

                return buffer;
            }

            internal static byte* Utf8EncodeHeap(string? value)
            {
                if (value is null)
                {
                    return null;
                }

                var bufferSize = Utf8Size(value);
                var buffer = (byte*)Marshal.AllocHGlobal(bufferSize);
                fixed (char* valuePtr = value)
                {
                    Encoding.UTF8.GetBytes(valuePtr, value.Length + 1, buffer, bufferSize);
                }

                return buffer;
            }

            public static string? UTF8_ToManaged(nint value, bool freePtr = false)
            {
                if (value == 0)
                {
                    return null;
                }

                var pointer = (byte*)value;
                var length = 0;
                while (pointer[length] != 0)
                {
                    length++;
                }

                var result = Encoding.UTF8.GetString(pointer, length);
                if (freePtr)
                {
                    Marshal.FreeHGlobal(value);
                }

                return result;
            }
        }

        """;
    }
}
```

Add initial category emitters with deterministic empty category output. Task 6 expands these same types when real declarations are collected from SDL2 headers:

```csharp
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emitting;

internal sealed class CsStructEmitter
{
    public string Emit(IReadOnlyList<BindingDeclaration> structs) => CsCodeGenerator.EmptyCategoryFile("Structs");
}

internal sealed class CsEnumEmitter
{
    public string Emit(IReadOnlyList<BindingDeclaration> enums) => CsCodeGenerator.EmptyCategoryFile("Enums");
}

internal sealed class CsConstantEmitter
{
    public string Emit(IReadOnlyList<BindingDeclaration> constants) => CsCodeGenerator.EmptyCategoryFile("Constants");
}

internal sealed class CsCallbackEmitter
{
    public string Emit(IReadOnlyList<BindingDeclaration> callbacks) => CsCodeGenerator.EmptyCategoryFile("Callbacks");
}
```

- [ ] **Step 5: Run emitter tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Expected: tests pass.

- [ ] **Step 6: Commit checkpoint**

Proposed commit message:

```text
feat: emit SDL2 core binding source
```

Before committing, present the summary and proposed message to Deniz and wait for approval.

---

## Task 6: Generate Real SDL2.Core Output from CppAst

**Files:**

- Modify: `build\_build\Targets\GenerateBindings\Program.cs`
- Create: `build\_build\Targets\GenerateBindings\Sdl2CoreGenerationConfig.cs`
- Create: `build\_build\Targets\GenerateBindings\FamilyGenerationConfig.cs`
- Create: `build\_build\Targets\GenerateBindings\BindingGenerationResult.cs`
- Modify: `build\_build\Targets\GenerateBindings\Model\DeclarationCollector.cs`
- Modify: `build\_build\Targets\GenerateBindings\Emitting\*.cs`
- Create/replace: `src\SDL2.Core\Generated\*.g.cs`
- Create: `src\SDL2.Core\Generated\.generated-stamp`
- Create: `src\SDL2.Core\Generated\UnsupportedDeclarations.g.json`
- Create: `src\SDL2.Core\Generated\PlatformAudit.g.json`

- [ ] **Step 1: Write failing end-to-end generation test with synthetic headers**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\GeneratorEndToEndTests.cs`:

```csharp
using Build.Targets.GenerateBindings.Cli;

namespace Build.Tests.Unit.Targets.GenerateBindings;

public sealed class GeneratorEndToEndTests
{
    [Test]
    public async Task Program_Should_Generate_Core_Files_From_Minimal_Sdl_Header_Set()
    {
        using var workspace = new TestWorkspace();
        var vcpkgInstalled = Path.Combine(workspace.Root.FullName, "vcpkg_installed");
        var includeRoot = Path.Combine(vcpkgInstalled, "x64-linux-hybrid", "include", "SDL2");
        Directory.CreateDirectory(includeRoot);

        foreach (var header in Build.Targets.GenerateBindings.HeaderSet.HeaderSetResolver.Sdl2CoreHeaders)
        {
            File.WriteAllText(Path.Combine(includeRoot, header), header == "SDL.h"
                ? """
                  typedef unsigned int Uint32;
                  typedef int SDL_bool;
                  typedef struct SDL_Window SDL_Window;
                  extern int SDL_Init(Uint32 flags);
                  extern void SDL_Quit(void);
                  extern SDL_Window* SDL_CreateWindow(const char *title, int x, int y, int w, int h, Uint32 flags);
                  extern void SDL_DestroyWindow(SDL_Window *window);
                  """
                : "/* empty */");
        }

        var output = Path.Combine(workspace.Root.FullName, "Generated");
        var request = new GenerateBindingsRequest(
            Config: Sdl2CoreGenerationConfig.Default,
            VcpkgInstalledDirectory: new DirectoryInfo(vcpkgInstalled),
            Triplet: "x64-linux-hybrid",
            OutputDirectory: new DirectoryInfo(output));

        // Build the runner from real (non-Cake) collaborators. The Cake-aware GenerateBindingsTask
        // shell is unit-tested separately under build\_build.Tests\Scenarios\GenerateBindings.
        var runner = TestRunnerFactory.Create();
        runner.Run(FakeCakeContext.Quiet(), request);

        await Assert.That(File.Exists(Path.Combine(output, "Commands.g.cs"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(output, "Handles.g.cs"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(output, ".generated-stamp"))).IsTrue();
        await Assert.That(File.ReadAllText(Path.Combine(output, "Commands.g.cs"))).Contains("SDL_Init");
    }
}
```

`TestRunnerFactory.Create()` is a small fixture helper that news up the pure collaborators directly (no DI container needed in tests). `FakeCakeContext.Quiet()` is a no-op `ICakeContext` for tests that exercise the runner end-to-end without needing the Cake-aware shell. Both helpers join the canonical `FakeCakeWorld`/`TargetTestHost` infrastructure in `build\_build.Tests\TestInfrastructure\`.

Run:

```pwsh
dotnet test build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because the full in-process generation pipeline is not yet implemented end-to-end (individual collaborators exist from Tasks 2-5 but their composition isn't wired through `BindingGenerationRunner` yet).

- [ ] **Step 2: Implement generation coordinator**

Create `build\_build\Targets\GenerateBindings\BindingGenerationResult.cs`:

```csharp
namespace Build.Targets.GenerateBindings;

internal sealed record BindingGenerationResult(int ExitCode, string Message);
```

Create `build\_build\Targets\GenerateBindings\FamilyGenerationConfig.cs`:

```csharp
namespace Build.Targets.GenerateBindings;

internal sealed record FamilyGenerationConfig(
    string Family,
    string LibraryName,
    string Namespace,
    string StaticClassName);
```

Create `build\_build\Targets\GenerateBindings\Sdl2CoreGenerationConfig.cs`:

```csharp
namespace Build.Targets.GenerateBindings;

internal static class Sdl2CoreGenerationConfig
{
    public static FamilyGenerationConfig Create() =>
        new("sdl2-core", "SDL2", "SDL2", "SDL");
}
```

Create `build\_build\Targets\GenerateBindings\BindingGenerationPipeline.cs` — the pure (Cake-free) end-to-end orchestrator. This is the class the Cake-aware `BindingGenerationRunner` (from Task 8) composes. There is no separate console app and no `Cli/` subdirectory.

```csharp
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Stamps;

namespace Build.Targets.GenerateBindings;

internal sealed class BindingGenerationPipeline(
    HeaderSetResolver headerSetResolver,
    CppAstParseRunner parseRunner,
    PlatformCatalog platformCatalog,
    DeclarationCollector declarationCollector,
    DeclarationMergePolicy mergePolicy,
    CsCodeGenerator codeGenerator,
    GeneratedStampWriter stampWriter)
{
    public BindingGenerationResult Run(GenerateBindingsRequest request, DirectoryInfo repoRoot)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(repoRoot);

        var headerSet = headerSetResolver.ResolveCoreHeaders(request.VcpkgInstalledDirectory, request.Triplet);
        var fingerprint = HeaderSetFingerprint.Compute(headerSet);
        var vcpkgManifestPath = Path.Combine(repoRoot.FullName, "vcpkg.json");
        var manifestPath = Path.Combine(repoRoot.FullName, "build", "manifest.json");

        var catalog = platformCatalog.GetSdl2CoreCatalog();
        var neutralView = catalog.ParseViews.Single(view => view.Name == "Neutral");
        var neutralParse = parseRunner.Parse(headerSet, neutralView);
        var neutralDeclarations = declarationCollector.Collect(neutralParse);

        var platformDeclarations = new List<BindingDeclaration>();
        foreach (var view in catalog.ParseViews.Where(view => view.Name != "Neutral"))
        {
            var parse = parseRunner.Parse(headerSet, view);
            platformDeclarations.AddRange(declarationCollector.Collect(parse));
        }

        var model = mergePolicy.Merge(neutralDeclarations, platformDeclarations);
        var generated = codeGenerator.Generate(model, request.Config);

        WriteFiles(request.OutputDirectory, generated);
        stampWriter.Write(request.OutputDirectory, new GeneratedStamp(
            SchemaVersion: 1,
            Family: request.Config.Family,
            GeneratorAssembly: "Build.Targets.GenerateBindings",
            CppAstVersion: "0.24.0",
            LibClangVersion: "20.1.2",
            VcpkgTriplet: request.Triplet,
            VcpkgManifestHash: ComputeFileHash(vcpkgManifestPath),
            VcpkgBaseline: ReadVcpkgBaseline(vcpkgManifestPath),
            ManifestLibraryVersion: ReadSdl2ManifestVersion(manifestPath),
            HeaderFingerprint: fingerprint.Hash,
            HeaderCount: fingerprint.HeaderCount,
            ParseViews: catalog.ParseViews.Select(view => view.Name).ToArray()));

        return new BindingGenerationResult(0, $"Generated {generated.Files.Count} files for {request.Config.Family}.");
    }

    private static void WriteFiles(DirectoryInfo outputDirectory, GeneratedFileSet generated)
    {
        if (outputDirectory.Exists)
        {
            outputDirectory.Delete(recursive: true);
        }

        outputDirectory.Create();
        foreach (var file in generated.Files)
        {
            var path = Path.Combine(outputDirectory.FullName, file.Key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Value);
        }
    }

    private static string ComputeFileHash(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string ReadVcpkgBaseline(string vcpkgManifestPath)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(vcpkgManifestPath));
        return document.RootElement.GetProperty("builtin-baseline").GetString()
            ?? throw new InvalidOperationException("vcpkg.json is missing builtin-baseline.");
    }

    private static string ReadSdl2ManifestVersion(string manifestPath)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));
        foreach (var library in document.RootElement.GetProperty("library_manifests").EnumerateArray())
        {
            if (string.Equals(library.GetProperty("vcpkg_name").GetString(), "sdl2", StringComparison.Ordinal))
            {
                return library.GetProperty("vcpkg_version").GetString()
                    ?? throw new InvalidOperationException("manifest.json sdl2 library manifest is missing vcpkg_version.");
            }
        }

        throw new InvalidOperationException("manifest.json is missing library_manifests entry for vcpkg_name 'sdl2'.");
    }
}
```

The `BindingGenerationRunner` (from Task 8) is the Cake-aware shell that owns the `ICakeContext` interaction; it delegates to `BindingGenerationPipeline.Run(request, repoRoot)` for the real work. Tests of the pipeline construct it directly without a Cake context (see Task 6 Step 1 — `TestRunnerFactory.Create()` returns a pipeline-with-runner pair).

**No `Program.cs` replacement.** Stage 1 does not introduce a standalone executable for the generator. The Cake host's existing `build/_build/Program.cs` (the Frosting entry point) is the only `Program.cs` in scope, and Task 1 already wired `AddGenerateBindings()` into its composition root.

- [ ] **Step 3: Implement DeclarationCollector for functions first**

Create `build\_build\Targets\GenerateBindings\Model\DeclarationCollector.cs`:

```csharp
using CppAst;
using Build.Targets.GenerateBindings.Parsing;

namespace Build.Targets.GenerateBindings.Model;

internal sealed class DeclarationCollector(TypeMappingPolicy typeMappingPolicy, KnownUnsupportedDeclarationPolicy unsupportedDeclarationPolicy)
{
    private readonly TypeMappingPolicy _typeMappingPolicy = typeMappingPolicy ?? throw new ArgumentNullException(nameof(typeMappingPolicy));
    private readonly KnownUnsupportedDeclarationPolicy _unsupportedDeclarationPolicy = unsupportedDeclarationPolicy ?? throw new ArgumentNullException(nameof(unsupportedDeclarationPolicy));

    public IReadOnlyList<BindingDeclaration> Collect(CppAstParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        return parseResult.Compilation.Functions
            .Where(function => function.SourceFile is not null)
            .Where(function => !string.IsNullOrWhiteSpace(function.Name))
            .Where(function => _unsupportedDeclarationPolicy.TryGetReason(function.Name) is null)
            .Select(function => BindingDeclaration.Function(
                Name: function.Name,
                ReturnType: MapType(function.ReturnType),
                Parameters: function.Parameters.Select(parameter => new BindingParameter(SafeIdentifier(parameter.Name), MapType(parameter.Type))).ToArray(),
                SourceHeader: Path.GetFileName(function.SourceFile!),
                ParseView: parseResult.ParseView.Name,
                SupportedOsPlatform: parseResult.ParseView.SupportedOsPlatform))
            .OrderBy(function => function.SourceHeader, StringComparer.Ordinal)
            .ThenBy(function => function.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private BindingTypeRef MapType(CppType type)
    {
        while (type is CppQualifiedType qualified)
        {
            type = qualified.ElementType;
        }

        return type switch
        {
            CppPrimitiveType primitive => MapPrimitive(primitive),
            CppPointerType pointer => MapPointer(pointer),
            CppTypedef typedef => _typeMappingPolicy.MapKnownTypedef(typedef.Name),
            CppClass cppClass => new(cppClass.Name, cppClass.Name, false, false, false),
            _ => throw new InvalidOperationException($"No SDL2.Core type mapping exists for CppAst type '{type}'.")
        };
    }

    private BindingTypeRef MapPointer(CppPointerType pointer)
    {
        var element = pointer.ElementType;
        while (element is CppQualifiedType qualified)
        {
            element = qualified.ElementType;
        }

        return element switch
        {
            CppPrimitiveType { Kind: CppPrimitiveKind.Void } => new("nint", "void*", true, false, false),
            CppPrimitiveType { Kind: CppPrimitiveKind.Char } => new("byte*", "char*", true, false, true),
            CppTypedef typedef => _typeMappingPolicy.MapKnownTypedefPointer(typedef.Name),
            CppClass cppClass => _typeMappingPolicy.MapKnownTypedefPointer(cppClass.Name),
            _ => throw new InvalidOperationException($"No SDL2.Core pointer mapping exists for CppAst type '{element}'.")
        };
    }

    private static BindingTypeRef MapPrimitive(CppPrimitiveType primitive) => primitive.Kind switch
    {
        CppPrimitiveKind.Void => new("void", "void", false, false, false),
        CppPrimitiveKind.Bool => new("byte", "bool", false, false, false),
        CppPrimitiveKind.Char => new("sbyte", "char", false, false, false),
        CppPrimitiveKind.Short => new("short", "short", false, false, false),
        CppPrimitiveKind.Int => new("int", "int", false, false, false),
        CppPrimitiveKind.Long => new("int", "long", false, false, false),
        CppPrimitiveKind.LongLong => new("long", "long long", false, false, false),
        CppPrimitiveKind.UnsignedChar => new("byte", "unsigned char", false, false, false),
        CppPrimitiveKind.UnsignedShort => new("ushort", "unsigned short", false, false, false),
        CppPrimitiveKind.UnsignedInt => new("uint", "unsigned int", false, false, false),
        CppPrimitiveKind.UnsignedLong => new("uint", "unsigned long", false, false, false),
        CppPrimitiveKind.UnsignedLongLong => new("ulong", "unsigned long long", false, false, false),
        CppPrimitiveKind.Float => new("float", "float", false, false, false),
        CppPrimitiveKind.Double => new("double", "double", false, false, false),
        _ => throw new InvalidOperationException($"No SDL2.Core primitive mapping exists for '{primitive.Kind}'.")
    };

    private static string SafeIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "value";
        }

        return name switch
        {
            "event" or "params" or "ref" or "out" or "in" or "object" or "string" => "@" + name,
            _ => name
        };
    }
}
```

- [ ] **Step 4: Extend emitters until generated real SDL2.Core compiles**

Run generation against local headers:

```pwsh
dotnet run --project build\_build\Build.csproj -- --target GenerateBindings --family sdl2-core
```

Then run:

```pwsh
dotnet build src\SDL2.Core\SDL2.Core.csproj -c Release
```

Expected first run: build fails with unmapped types or unsupported declarations. For each failure, add a focused mapping or emitter test first, then implement the exact mapping. Do not add a catch-all `nint` fallback for unknown public types. Continue until generated SDL2.Core compiles for all target frameworks.

Complete this emitted-category checklist before leaving this step:

- Constants: emit numeric/string macros into `Constants.g.cs`, distinguish literal `const` values from computed `static readonly` values, and add tests for `SDL_INIT_VIDEO`, `SDL_WINDOWPOS_CENTERED`, and one string literal.
- Enums: emit explicit underlying types and `[Flags]` where values are bit flags; add tests for `SDL_WindowFlags`, `SDL_InitFlags`, and a non-flags enum such as `SDL_PowerState`.
- Structs/unions: emit `[StructLayout]` for POD structs; add tests for `SDL_version`, `SDL_Rect`, and `SDL_Event`. **`SDL_SysWMinfo` and `SDL_SysWMmsg` are not emitted as typed structs in Stage 1** — they are listed in `KnownUnsupportedDeclarationPolicy` with category `deferred-to-stage-2` and recorded in `UnsupportedDeclarations.g.json`. Stage 2 takes on the typed union layout after the forward-declaration stub library lands.
- Handles: emit every opaque SDL core pointer as a typed nested handle; add tests for `SDL_Window`, `SDL_Renderer`, `SDL_Texture`, `SDL_Surface`, and `SDL_RWops`.
- Callbacks: emit concrete function-pointer signatures where C signatures are representable; add tests for `SDL_AudioCallback` and `SDL_WindowsMessageHook`.
- Commands: emit dual `[LibraryImport]` / `[DllImport]` branches in the same function path; add tests for `SDL_Init`, `SDL_Quit`, `SDL_CreateWindow`, `SDL_DestroyWindow`, and `SDL_GetError`.
- Friendly overloads: emit `string`, `ReadOnlySpan<byte>`, `Span<T>`, `out`, and `ref` overloads when the raw signature has a known pattern; add tests for `SDL_CreateWindow`, `SDL_GetVersion(out SDL_version)`, and `SDL_PollEvent(out SDL_Event)`.
- Platform metadata: emit `[SupportedOSPlatform]` for OS-conditioned commands and preserve backend condition metadata in `PlatformAudit.g.json`; add tests for one Windows, Linux, macOS, Android, iOS, and backend-only declaration.
- C type provenance: include native declaration names in generated comments or adjacent metadata for functions, structs, callbacks, and enums; add a test that `SDL_CreateWindow` carries `const char *title` provenance.

Minimum mappings that must be explicitly represented before this task is done:

```text
SDL_bool -> int
char* / const char* -> byte* raw plus string/ReadOnlySpan<byte> friendly overload where public
void* -> nint
SDL_Window* -> SDL_Window
SDL_Renderer* -> SDL_Renderer
SDL_Texture* -> SDL_Texture
SDL_Surface* -> SDL_Surface
SDL_RWops* -> SDL_RWops
SDL_version -> SDL_version struct
SDL_Rect / SDL_Point / SDL_Color / SDL_Palette / SDL_PixelFormat -> generated structs
SDL_Event and nested event payloads -> generated structs/unions
SDL_SysWMinfo -> NOT emitted as a typed struct in Stage 1 (deferred-to-stage-2); SDL_GetWindowWMInfo takes an opaque SDL_SysWMinfo* parameter modeled as nint
SDL_SysWMmsg  -> NOT emitted as a typed struct in Stage 1 (deferred-to-stage-2)
SDL_AudioCallback and other callbacks -> concrete function-pointer signatures such as delegate* unmanaged[Cdecl]<void*, byte*, int, void> where representable
```

**`SDL_syswm.h` is Stage 2.** Stage 1 emits `SDL_GetWindowWMInfo` as a `[LibraryImport]` / `[DllImport]` function pair with `SDL_SysWMinfo*` modeled as `nint` (opaque pointer). The function appears in the public surface and consumers can pass an `IntPtr.Zero` placeholder — they cannot construct or read the struct yet. The typed union with `[StructLayout(LayoutKind.Explicit, Size = 64)]`, all platform branch payload structs at `[FieldOffset(0)]`, the 64-byte fixed-size lock from `SDL_syswm.h:346-348`, and the ~15–20-type forward-declaration stub library all land in Stage 2.

`KnownUnsupportedDeclarationPolicy` carries the deferral classification (per Task 4) and `UnsupportedDeclarations.g.json` records both `SDL_SysWMinfo` and `SDL_SysWMmsg` with category `deferred-to-stage-2` and a pointer to Stage 2 plan. The reproducibility gate must accept this state as the Stage 1 baseline.

- [ ] **Step 5: Emit unsupported declaration and platform audit artifacts**

Add generated JSON files:

`src\SDL2.Core\Generated\UnsupportedDeclarations.g.json`:

```json
{
  "family": "sdl2-core",
  "unsupported": [
    {
      "name": "SDL_Log",
      "category": "variadic",
      "reason": "C variadic function. Stage 1 does not emit success-shaped P/Invoke for native varargs."
    },
    {
      "name": "SDL_SysWMinfo",
      "category": "deferred-to-stage-2",
      "reason": "Typed union layout requires the forward-declaration stub library (HWND, IInspectable, Display, Window, NSWindow, UIWindow, wl_display, gbm_device, IDirectFB, EGLNativeDisplayType, ANativeWindow, ...). Stage 2 plan introduces the stub library and emits the typed [StructLayout(LayoutKind.Explicit, Size = 64)] union with platform branches at [FieldOffset(0)] per SDL_syswm.h:346-348."
    },
    {
      "name": "SDL_SysWMmsg",
      "category": "deferred-to-stage-2",
      "reason": "Same as SDL_SysWMinfo — platform-conditioned union, Stage 2 work."
    }
  ]
}
```

`src\SDL2.Core\Generated\PlatformAudit.g.json`:

```json
{
  "family": "sdl2-core",
  "parse_views": [
    "Neutral",
    "WindowsDesktop",
    "WinRT",
    "GDK",
    "Linux",
    "MacOS",
    "IOS",
    "Android"
  ],
  "proof_headers": [
    "SDL_system.h",
    "SDL_main.h",
    "SDL_syswm.h",
    "SDL_platform.h",
    "SDL_stdinc.h"
  ],
  "documented_exclusions": [
    {
      "kind": "backend",
      "names": ["DirectFB", "Vivante", "MIR", "OS/2"],
      "reason": "Niche/deprecated SDL2 video drivers; Stage 1 does not include separate parse views. Revisit on consumer ask."
    },
    {
      "kind": "operating-system",
      "names": ["TvOS"],
      "reason": "tvOS surface differs only marginally from iOS for SDL2 public functions; defer dedicated pass to Stage 2 if a consumer specifically targets tvOS."
    }
  ]
}
```

The actual generated files may contain more unsupported declarations and metadata, but every unsupported entry must include a concrete declaration name and reason.

- [ ] **Step 6: Run real generation and compile all Core TFMs**

Run:

```pwsh
dotnet run --project build\_build\Build.csproj -- --target GenerateBindings --family sdl2-core
dotnet build src\SDL2.Core\SDL2.Core.csproj -c Release
```

Expected: generator exits `0`, generated files exist, and SDL2.Core builds across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, and `net462`.

- [ ] **Step 7: Run reproducibility diff**

Run the generator twice:

```pwsh
dotnet run --project build\_build\Build.csproj -- --target GenerateBindings --family sdl2-core
dotnet run --project build\_build\Build.csproj -- --target GenerateBindings --family sdl2-core
git --no-pager diff -- src\SDL2.Core\Generated
```

Expected: second run produces no diff.

- [ ] **Step 8: Commit checkpoint**

Proposed commit message:

```text
feat: generate SDL2 core bindings
```

Before committing, present the summary and proposed message to Deniz and wait for approval.

---

## Task 7: Wire SDL2.Core Away From external\sdl2-cs and Update Consumers for Typed Handles

**Files:**

- Modify: `src\SDL2.Core\SDL2.Core.csproj`
- Modify: `tests\smoke-tests\package-smoke\PackageConsumer.Smoke\PackageSmokeTests.cs`
- Modify: `tests\Sandbox\Program.cs`

- [ ] **Step 1: Write/confirm compile failure boundary**

Temporarily remove the `external\sdl2-cs` compile include from `src\SDL2.Core\SDL2.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>Janset.SDL2.Core</PackageId>
    <Description>Core C# bindings for SDL2</Description>
    <TargetFrameworks>$(TargetFrameworks)</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\native\SDL2.Core.Native\SDL2.Core.Native.csproj"
                      IncludeAssets="all"
                      PrivateAssets="none"/>
  </ItemGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>SDL2.Image</_Parameter1>
    </AssemblyAttribute>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>SDL2.Gfx</_Parameter1>
    </AssemblyAttribute>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>SDL2.Mixer</_Parameter1>
    </AssemblyAttribute>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>SDL2.Ttf</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
```

Run:

```pwsh
dotnet build src\SDL2.Core\SDL2.Core.csproj -c Release
dotnet build src\SDL2.Image\SDL2.Image.csproj -c Release
dotnet build src\SDL2.Mixer\SDL2.Mixer.csproj -c Release
dotnet build src\SDL2.Ttf\SDL2.Ttf.csproj -c Release
dotnet build src\SDL2.Gfx\SDL2.Gfx.csproj -c Release
```

Expected: Core and every SDL2 satellite still compile after Core stops importing `external\sdl2-cs`. If a satellite fails because it still uses an SDL2-CS helper or shared Core type, return to Task 6 and emit the missing compatibility surface in generated Core; do not restore the `external\sdl2-cs` include.

- [ ] **Step 2: Update package smoke typed handle checks**

In `tests\smoke-tests\package-smoke\PackageConsumer.Smoke\PackageSmokeTests.cs`, replace Core handle `IntPtr.Zero` comparisons for generated Core handles:

```csharp
SDL.SDL_Surface surface = SDL.SDL_Surface.Null;
SDL.SDL_Renderer renderer = SDL.SDL_Renderer.Null;

surface = SDL.SDL_CreateRGBSurfaceWithFormat(0, 64, 64, 32, SDL.SDL_PIXELFORMAT_ARGB8888);
await Assert.That(surface.IsNull).IsFalse();

renderer = SDL.SDL_CreateSoftwareRenderer(surface);
await Assert.That(renderer.IsNull).IsFalse();

if (!renderer.IsNull)
{
    SDL.SDL_DestroyRenderer(renderer);
}

if (!surface.IsNull)
{
    SDL.SDL_FreeSurface(surface);
}
```

Keep satellite APIs that still return `IntPtr` unchanged until Stage 2.

- [ ] **Step 3: Update Sandbox typed handle checks**

In `tests\Sandbox\Program.cs`, change window checks:

```csharp
var window = SDL_CreateWindow(
    WindowTitle,
    SDL_WINDOWPOS_CENTERED,
    SDL_WINDOWPOS_CENTERED,
    WindowWidth,
    WindowHeight,
    SDL_WindowFlags.SDL_WINDOW_SHOWN);

if (window.IsNull)
{
    Console.WriteLine("SDL_CreateWindow failed: " + SDL_GetError());
    SDL_Quit();
    return 1;
}
```

The `finally` block remains:

```csharp
SDL_DestroyWindow(window);
SDL_Quit();
```

- [ ] **Step 4: Build affected projects**

Run:

```pwsh
dotnet build src\SDL2.Core\SDL2.Core.csproj -c Release
dotnet build src\SDL2.Image\SDL2.Image.csproj -c Release
dotnet build src\SDL2.Mixer\SDL2.Mixer.csproj -c Release
dotnet build src\SDL2.Ttf\SDL2.Ttf.csproj -c Release
dotnet build src\SDL2.Gfx\SDL2.Gfx.csproj -c Release
dotnet build tests\Sandbox\Sandbox.csproj -c Release
dotnet build tests\smoke-tests\package-smoke\PackageConsumer.Smoke\PackageConsumer.Smoke.csproj -c Release -p:LocalPackageFeed=artifacts\packages -p:JansetSdl2CorePackageVersion=0.0.0-smoke-sentinel -p:JansetSdl2ImagePackageVersion=0.0.0-smoke-sentinel -p:JansetSdl2MixerPackageVersion=0.0.0-smoke-sentinel -p:JansetSdl2TtfPackageVersion=0.0.0-smoke-sentinel -p:JansetSdl2GfxPackageVersion=0.0.0-smoke-sentinel
```

Expected: Core, all SDL2 satellites, and Sandbox build. The smoke direct build may fail at restore if local packages are not present; if so, record that the package-smoke compile is validated through Task 10 package-first smoke.

- [ ] **Step 5: Commit checkpoint**

Proposed commit message:

```text
feat: switch SDL2 core to generated bindings
```

Before committing, present the summary and proposed message to Deniz and wait for approval.

---

## Task 8: Cake GenerateBindings Target and PreFlight Stamp Coherence

**Files:**

- Create: `build\_build\Targets\GenerateBindings\*.cs`
- Modify: `build\_build\Program.cs`
- Modify: `build\_build\Validation\ServiceCollectionExtensions.cs`
- Create: `build\_build\Validation\BindingGeneration\*.cs`
- Modify: `build\_build\Targets\PreFlightCheck\PreFlightCheckTask.cs`
- Modify: `build\_build\Targets\PreFlightCheck\Reporting\PreflightReporter.cs`
- Modify: `build\_build.Tests\Unit\CompositionRoot\ServiceCollectionExtensionsSmokeTests.cs`
- Modify: `build\_build.Tests\Scenarios\PreFlightCheck\PreFlightCheckTaskScenarioTests.cs`
- Modify: `.github\workflows\release.yml`

- [ ] **Step 1: Invoke dependency-injection skill before build-host wiring**

Because this task adds target module DI wiring, invoke `dependency-injection-patterns` before implementation.

- [ ] **Step 2: Write failing GenerateBindings target scenario**

Create `build\_build.Tests\Scenarios\GenerateBindings\GenerateBindingsTaskScenarioTests.cs`:

```csharp
using Build.Data;
using Build.Targets.GenerateBindings;
using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class GenerateBindingsTaskScenarioTests
{
    [Test]
    public async Task RunAsync_Should_Invoke_Generator_For_Sdl2_Core()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithToolPath("dotnet", "C:/dotnet/dotnet.exe")
            .WithProcessResult("dotnet.exe", 0, "Generated 8 files for sdl2-core.");

        var result = await new TargetTestHost<GenerateBindingsTask>(world)
            .WithServices(services =>
            {
                services.AddData();
                services.AddGenerateBindings();
            })
            .RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.ProcessInvocations.Any(invocation =>
            invocation.Arguments.Render().Contains("--family sdl2-core", StringComparison.Ordinal))).IsTrue();
    }
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because GenerateBindings target does not exist.

- [ ] **Step 3: Implement GenerateBindings target**

Create `build\_build\Targets\GenerateBindings\GenerateBindingsOptions.cs`:

```csharp
using System.CommandLine;

namespace Build.Targets.GenerateBindings;

public static class GenerateBindingsOptions
{
    public static readonly Option<string> FamilyOption = new(
        name: "--family",
        getDefaultValue: () => "sdl2-core",
        description: "Binding family to generate. Stage 1 supports sdl2-core.");
}
```

Create `build\_build\Targets\GenerateBindings\GenerateBindingsRequest.cs`:

```csharp
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings;

internal sealed record GenerateBindingsRequest(
    string Family,
    DirectoryPath RepoRoot,
    DirectoryPath VcpkgInstalledDirectory,
    string Triplet,
    DirectoryPath OutputDirectory);
```

Create `build\_build\Targets\GenerateBindings\BindingGenerationRunner.cs` as an **in-process** orchestrator. The generator no longer spawns a separate process — it lives in the same Cake host, so the runner calls the pure emitter collaborators directly:

```csharp
using Cake.Core;
using Cake.Core.IO;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.Stamps;

namespace Build.Targets.GenerateBindings;

internal sealed class BindingGenerationRunner(
    HeaderSetResolver headerSetResolver,
    CppAstParseRunner parseRunner,
    PlatformCatalog platformCatalog,
    DeclarationCollector declarationCollector,
    DeclarationMergePolicy mergePolicy,
    CsCodeGenerator codeGenerator,
    GeneratedStampWriter stampWriter)
{
    public void Run(ICakeContext context, GenerateBindingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        // 1. Resolve canonical headers (vcpkg-installed SDL include tree under request.Triplet).
        var headerSet = headerSetResolver.ResolveCoreHeaders(
            new DirectoryInfo(request.VcpkgInstalledDirectory.FullPath),
            request.Triplet);

        // 2. Build parse views from the platform catalog (~7-8 entries for SDL2.Core in Stage 1).
        var parseViews = platformCatalog.GetParseViewsFor(request.Config);

        // 3. Run CppAst parse for each view; collect declarations into the generator-owned model.
        var bindingModel = new BindingModel();
        foreach (var view in parseViews)
        {
            var parseResult = parseRunner.Parse(headerSet, view);
            declarationCollector.CollectInto(bindingModel, parseResult, view);
        }

        // 4. Merge neutral + platform-conditioned declarations; fail closed on conflicts.
        mergePolicy.Merge(bindingModel);

        // 5. Emit C# code into the output directory.
        var outputDir = new DirectoryInfo(request.OutputDirectory.FullPath);
        codeGenerator.Emit(bindingModel, request.Config, outputDir);

        // 6. Write the deterministic .generated-stamp.
        stampWriter.Write(bindingModel, request, outputDir);

        context.Log.Information(
            "GenerateBindings: emitted {0} declarations across {1} parse views for {2}.",
            bindingModel.DeclarationCount,
            parseViews.Count,
            request.Config.Family);
    }
}
```

The pure emitter collaborators (`HeaderSetResolver`, `CppAstParseRunner`, `PlatformCatalog`, `DeclarationCollector`, `DeclarationMergePolicy`, `CsCodeGenerator`, `GeneratedStampWriter`) are constructed earlier-task by earlier-task and registered through `AddGenerateBindings()`. The runner is the Cake-aware shell that ties them together — see ADR-002 §2.2 (task-owned orchestration) + §"Pure code stays pure".

Create `build\_build\Targets\GenerateBindings\GenerateBindingsTask.cs` with the Linux-canonical host guard:

```csharp
using System.Runtime.InteropServices;
using Build.Host;
using Cake.Frosting;

namespace Build.Targets.GenerateBindings;

[TaskName("GenerateBindings")]
[TaskDescription("Regenerates committed binding source for the selected SDL family. Linux-canonical: fails closed on any other host RID.")]
public sealed class GenerateBindingsTask(BindingGenerationRunner runner) : AsyncFrostingTask<BuildContext>
{
    private readonly BindingGenerationRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Linux-canonical lock: the version-trio + preprocessor-macro switching strategy is only validated
        // inside the linux-builder container with a linux-x64 sysroot. Fail closed on any other host so a
        // maintainer cannot accidentally generate from a Windows or macOS dev box. tools.cs generate-bindings
        // is the supported local entry point; it provisions the container automatically.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.OSArchitecture != Architecture.X64)
        {
            throw new CakeException(
                "GenerateBindings is Linux-canonical (linux-x64 only). Detected host: " +
                $"{RuntimeInformation.OSDescription} / {RuntimeInformation.OSArchitecture}. " +
                "Use `tools.cs generate-bindings` from a Windows/macOS dev box — it runs this target inside the linux-builder container.");
        }

        if (!string.Equals(context.BindingFamily, "sdl2-core", StringComparison.OrdinalIgnoreCase))
        {
            throw new CakeException("GenerateBindings Stage 1 supports only --family sdl2-core.");
        }

        var request = new GenerateBindingsRequest(
            Config: Sdl2CoreGenerationConfig.Default,
            VcpkgInstalledDirectory: new DirectoryInfo(context.Paths.GetVcpkgInstalledDir.FullPath),
            Triplet: "x64-linux-hybrid", // canonical Stage 1 triplet
            OutputDirectory: new DirectoryInfo(context.Paths.RepoRoot.Combine("src/SDL2.Core/Generated").FullPath));

        _runner.Run(context, request);
        return Task.CompletedTask;
    }
}
```

Update `build\_build\Targets\GenerateBindings\ServiceCollectionExtensions.cs` (the placeholder from Task 1 is fleshed out here once all collaborators exist):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.Stamps;

namespace Build.Targets.GenerateBindings;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGenerateBindings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Pure collaborators — no Cake context, safe to instantiate as singletons.
        services.AddSingleton<HeaderSetResolver>();
        services.AddSingleton<CppAstParseRunner>();
        services.AddSingleton<PlatformCatalog>();
        services.AddSingleton<DeclarationCollector>();
        services.AddSingleton<DeclarationMergePolicy>();
        services.AddSingleton<CoreOwnedTypeMap>();
        services.AddSingleton<KnownUnsupportedDeclarationPolicy>();
        services.AddSingleton<TypeMappingPolicy>();
        services.AddSingleton<CsCodeGenerator>();
        services.AddSingleton<GeneratedStampWriter>();

        // Cake-aware shell — composes the pure collaborators above and translates to Cake logging/exception.
        services.AddSingleton<BindingGenerationRunner>();

        return services;
    }
}
```

Modify `build\_build\Program.cs`:

```csharp
using Build.Targets.GenerateBindings;
```

Add the option:

```csharp
root.AddOption(GenerateBindingsOptions.FamilyOption);
```

Add the DI registration:

```csharp
.AddGenerateBindings()
```

Extend `ParsedArguments` and `BuildContext` with `BindingFamily`. Follow existing CLI property patterns; do not store target state in `FrostingLifetime`.

- [ ] **Step 4: Write failing PreFlight stamp coherence scenario**

Extend `build\_build.Tests\Scenarios\PreFlightCheck\PreFlightCheckTaskScenarioTests.cs` with:

```csharp
[Test]
public async Task RunAsync_Should_Throw_When_Binding_Generated_Stamp_Is_Missing_For_Core()
{
    var manifest = ManifestFixture.CreateTestManifestConfig();
    var world = FakeCakeWorld.CreateWindows()
        .WithManifestObject(manifest)
        .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
        .WithTextFile("vcpkg-overlay-triplets/x64-linux-hybrid.cmake", "# overlay")
        .WithVersionsFile(VersionsFilePath)
        .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-valid.json"));

    SeedCsprojsForManifestFixture(world);

    var result = await CreateHost(world).RunAsync();

    await Assert.That(result.Success).IsFalse();
    await Assert.That(result.Exception!.Message).Contains("Pre-flight check failed");
    await Assert.That(result.Log.HasMessage(LogLevel.Error, "binding generation coherence")).IsTrue();
}
```

Add companion failing scenarios for each coherence axis before implementing the validator:

```csharp
[Test]
public async Task RunAsync_Should_Throw_When_Binding_Stamp_Vcpkg_Manifest_Hash_Drifts()
{
    var world = CreatePreFlightWorldWithGeneratedStamp(stamp => stamp with
    {
        VcpkgManifestHash = "sha256:not-current"
    });

    var result = await CreateHost(world).RunAsync();

    await Assert.That(result.Success).IsFalse();
    await Assert.That(result.Log.HasMessage(LogLevel.Error, "VcpkgManifestHash")).IsTrue();
}

[Test]
public async Task RunAsync_Should_Throw_When_Binding_Stamp_Baseline_Drifts()
{
    var world = CreatePreFlightWorldWithGeneratedStamp(stamp => stamp with
    {
        VcpkgBaseline = "not-current"
    });

    var result = await CreateHost(world).RunAsync();

    await Assert.That(result.Success).IsFalse();
    await Assert.That(result.Log.HasMessage(LogLevel.Error, "VcpkgBaseline")).IsTrue();
}

[Test]
public async Task RunAsync_Should_Throw_When_Binding_Stamp_Manifest_Library_Version_Drifts()
{
    var world = CreatePreFlightWorldWithGeneratedStamp(stamp => stamp with
    {
        ManifestLibraryVersion = "0.0.0"
    });

    var result = await CreateHost(world).RunAsync();

    await Assert.That(result.Success).IsFalse();
    await Assert.That(result.Log.HasMessage(LogLevel.Error, "ManifestLibraryVersion")).IsTrue();
}

[Test]
public async Task RunAsync_Should_Throw_When_Binding_Stamp_Header_Fingerprint_Drifts()
{
    var world = CreatePreFlightWorldWithGeneratedStamp(stamp => stamp with
    {
        HeaderFingerprint = "sha256:not-current"
    });

    var result = await CreateHost(world).RunAsync();

    await Assert.That(result.Success).IsFalse();
    await Assert.That(result.Log.HasMessage(LogLevel.Error, "HeaderFingerprint")).IsTrue();
}

[Test]
public async Task RunAsync_Should_Throw_When_Binding_Stamp_Parse_Views_Drift()
{
    var world = CreatePreFlightWorldWithGeneratedStamp(stamp => stamp with
    {
        ParseViews = ["Neutral", "Windows", "Linux"]
    });

    var result = await CreateHost(world).RunAsync();

    await Assert.That(result.Success).IsFalse();
    await Assert.That(result.Log.HasMessage(LogLevel.Error, "ParseViews")).IsTrue();
}
```

Add fixture helpers in the same test class. The expected "current" stamp is built from the same fixture content, not from hard-coded hashes copied by hand:

```csharp
private static FakeCakeWorld CreatePreFlightWorldWithGeneratedStamp(
    Func<BindingGeneratedStampFixture, BindingGeneratedStampFixture> mutate)
{
    var manifest = ManifestFixture.CreateTestManifestConfig();
    var vcpkgJson = FixtureLoader.Load("Vcpkg/vcpkg-valid.json");
    var headers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SDL.h"] = "#pragma once\n#define SDL_INIT_VIDEO 0x00000020u\n",
        ["SDL_version.h"] = "#pragma once\n#define SDL_MAJOR_VERSION 2\n"
    };

    var stamp = mutate(BindingGeneratedStampFixture.CreateCurrent(vcpkgJson, manifest, headers));
    var world = FakeCakeWorld.CreateWindows()
        .WithManifestObject(manifest)
        .WithTextFile("vcpkg.json", vcpkgJson)
        .WithTextFile("vcpkg-overlay-triplets/x64-linux-hybrid.cmake", "# overlay")
        .WithVersionsFile(VersionsFilePath)
        .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-valid.json"))
        .WithTextFile("src/SDL2.Core/Generated/.generated-stamp", JsonSerializer.Serialize(stamp, JsonOptions));

    foreach (var header in headers)
    {
        world.WithTextFile($"vcpkg_installed/x64-linux-hybrid/include/SDL2/{header.Key}", header.Value);
    }

    SeedCsprojsForManifestFixture(world);
    return world;
}

private sealed record BindingGeneratedStampFixture(
    int SchemaVersion,
    string Family,
    string GeneratorAssembly,
    string CppAstVersion,
    string LibClangVersion,
    string VcpkgTriplet,
    string VcpkgManifestHash,
    string VcpkgBaseline,
    string ManifestLibraryVersion,
    string HeaderFingerprint,
    int HeaderCount,
    string[] ParseViews)
{
    public static BindingGeneratedStampFixture CreateCurrent(
        string vcpkgJson,
        ManifestConfig manifest,
        IReadOnlyDictionary<string, string> headers) =>
        new(
            SchemaVersion: 1,
            Family: "sdl2-core",
            GeneratorAssembly: "Build.Targets.GenerateBindings",
            CppAstVersion: "0.24.0",
            LibClangVersion: "20.1.2",
            VcpkgTriplet: "x64-linux-hybrid",
            VcpkgManifestHash: ComputeFixtureFileHash(vcpkgJson),
            VcpkgBaseline: JsonDocument.Parse(vcpkgJson).RootElement.GetProperty("builtin-baseline").GetString()!,
            ManifestLibraryVersion: manifest.LibraryManifests.Single(library => library.VcpkgName == "sdl2").VcpkgVersion,
            HeaderFingerprint: ComputeFixtureHeaderFingerprint(headers),
            HeaderCount: headers.Count,
            ParseViews: [.. BindingGenerationStampContract.ExpectedSdl2CoreParseViews]);
}

private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
{
    WriteIndented = true
};

private static string ComputeFixtureFileHash(string content)
{
    var bytes = Encoding.UTF8.GetBytes(content);
    return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

private static string ComputeFixtureHeaderFingerprint(IReadOnlyDictionary<string, string> headers)
{
    using var hash = SHA256.Create();
    foreach (var header in headers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
    {
        var nameBytes = Encoding.UTF8.GetBytes(header.Key);
        hash.TransformBlock(nameBytes, 0, nameBytes.Length, null, 0);
        var contentBytes = Encoding.UTF8.GetBytes(header.Value);
        hash.TransformBlock(contentBytes, 0, contentBytes.Length, null, 0);
    }

    hash.TransformFinalBlock([], 0, 0);
    return "sha256:" + Convert.ToHexString(hash.Hash!).ToLowerInvariant();
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because coherence validator does not exist.

- [ ] **Step 5: Implement binding generation coherence validator**

Create `build\_build\Validation\BindingGeneration\BindingGenerationCoherenceReport.cs`:

```csharp
namespace Build.Validation.BindingGeneration;

public sealed record BindingGenerationCoherenceReport(bool IsValid, IReadOnlyList<string> Errors)
{
    public bool HasErrors => Errors.Count > 0;
    public static BindingGenerationCoherenceReport Valid { get; } = new(true, []);
    public static BindingGenerationCoherenceReport Invalid(params string[] errors) => new(false, errors);
}
```

Create `build\_build\Validation\BindingGeneration\IBindingGenerationCoherenceValidator.cs`:

```csharp
using Build.Data.Manifest.Models;
using Cake.Core.IO;

namespace Build.Validation.BindingGeneration;

public interface IBindingGenerationCoherenceValidator
{
    BindingGenerationCoherenceReport Validate(
        ManifestConfig manifest,
        VcpkgManifest vcpkgManifest,
        DirectoryPath repoRoot,
        DirectoryPath vcpkgInstalledDirectory,
        string triplet);
}
```

Create `build\_build\Validation\BindingGeneration\BindingGenerationCoherenceValidator.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.Json;
using Build.Data.Manifest.Models;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Validation.BindingGeneration;

public sealed class BindingGenerationCoherenceValidator(ICakeContext context) : IBindingGenerationCoherenceValidator
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public BindingGenerationCoherenceReport Validate(
        ManifestConfig manifest,
        VcpkgManifest vcpkgManifest,
        DirectoryPath repoRoot,
        DirectoryPath vcpkgInstalledDirectory,
        string triplet)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(vcpkgManifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);

        var coreFamily = manifest.PackageFamilies.SingleOrDefault(family =>
            string.Equals(family.Name, "sdl2-core", StringComparison.Ordinal));

        if (coreFamily is null || string.IsNullOrWhiteSpace(coreFamily.ManagedProject))
        {
            return BindingGenerationCoherenceReport.Valid;
        }

        var stampPath = repoRoot.CombineWithFilePath("src/SDL2.Core/Generated/.generated-stamp");
        if (!_context.FileExists(stampPath))
        {
            return BindingGenerationCoherenceReport.Invalid("binding generation coherence: src/SDL2.Core/Generated/.generated-stamp is missing. Run GenerateBindings for sdl2-core.");
        }

        using var document = JsonDocument.Parse(ReadAllText(stampPath));
        var root = document.RootElement;
        var expectedHeaders = ComputeHeaderFingerprint(vcpkgInstalledDirectory.Combine(triplet).Combine("include").Combine("SDL2"));
        var expectedParseViews = BindingGenerationStampContract.ExpectedSdl2CoreParseViews.Order(StringComparer.Ordinal).ToArray();
        var actualParseViews = root.GetProperty("ParseViews").EnumerateArray().Select(value => value.GetString()).Where(value => value is not null).Order(StringComparer.Ordinal).ToArray();

        var errors = new List<string>();
        Require(root, "Family", "sdl2-core", errors);
        Require(root, "CppAstVersion", "0.24.0", errors);
        Require(root, "LibClangVersion", "20.1.2", errors);
        Require(root, "VcpkgTriplet", triplet, errors);
        Require(root, "VcpkgManifestHash", HashFile(repoRoot.CombineWithFilePath("vcpkg.json")), errors);
        Require(root, "VcpkgBaseline", vcpkgManifest.BuiltinBaseline, errors);
        Require(root, "ManifestLibraryVersion", ResolveSdl2LibraryVersion(manifest), errors);
        Require(root, "HeaderFingerprint", expectedHeaders.Hash, errors);
        Require(root, "HeaderCount", expectedHeaders.Count, errors);

        if (!actualParseViews.SequenceEqual(expectedParseViews, StringComparer.Ordinal))
        {
            errors.Add("binding generation coherence: SDL2.Core stamp ParseViews must match the generator platform catalog.");
        }

        return errors.Count == 0
            ? BindingGenerationCoherenceReport.Valid
            : BindingGenerationCoherenceReport.Invalid([.. errors]);
    }

    private static void Require(JsonElement root, string propertyName, string expected, List<string> errors)
    {
        var actual = root.GetProperty(propertyName).GetString();
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            errors.Add($"binding generation coherence: SDL2.Core stamp {propertyName} must be {expected}.");
        }
    }

    private static void Require(JsonElement root, string propertyName, int expected, List<string> errors)
    {
        var actual = root.GetProperty(propertyName).GetInt32();
        if (actual != expected)
        {
            errors.Add($"binding generation coherence: SDL2.Core stamp {propertyName} must be {expected}.");
        }
    }

    private string HashFile(FilePath path)
    {
        using var stream = _context.FileSystem.GetFile(path).OpenRead();
        return "sha256:" + Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private HeaderFingerprint ComputeHeaderFingerprint(DirectoryPath headerDirectory)
    {
        if (!_context.DirectoryExists(headerDirectory))
        {
            throw new CakeException($"SDL2 header directory was not found: {headerDirectory.FullPath}");
        }

        var files = _context.GetFiles(headerDirectory.CombineWithFilePath("*.h").FullPath)
            .Select(path => path.FullPath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        using var hash = SHA256.Create();
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(headerDirectory.FullPath, file).Replace(Path.DirectorySeparatorChar, '/');
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(relative);
            hash.TransformBlock(nameBytes, 0, nameBytes.Length, null, 0);
            using var fileStream = _context.FileSystem.GetFile(new FilePath(file)).OpenRead();
            var bytes = new byte[fileStream.Length];
            fileStream.ReadExactly(bytes);
            hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        hash.TransformFinalBlock([], 0, 0);
        return new HeaderFingerprint("sha256:" + Convert.ToHexString(hash.Hash!).ToLowerInvariant(), files.Length);
    }

    private string ReadAllText(FilePath path)
    {
        using var reader = new StreamReader(_context.FileSystem.GetFile(path).OpenRead());
        return reader.ReadToEnd();
    }

    private static string ResolveSdl2LibraryVersion(ManifestConfig manifest) =>
        manifest.LibraryManifests.Single(library => string.Equals(library.VcpkgName, "sdl2", StringComparison.Ordinal)).VcpkgVersion;

    private sealed record HeaderFingerprint(string Hash, int Count);
}
```

Create `build\_build\Validation\BindingGeneration\BindingGenerationStampContract.cs` with the validator-side expected SDL2 Core parse-view names. Keep this list in the same order as the generator catalog:

```csharp
namespace Build.Validation.BindingGeneration;

internal static class BindingGenerationStampContract
{
    public static IReadOnlyList<string> ExpectedSdl2CoreParseViews { get; } =
    [
        "Neutral",
        "WindowsDesktop",
        "WinRT",
        "GDK",
        "Linux",
        "MacOS",
        "IOS",
        "Android",
    ];
}
```

These eight names must match the `PlatformCatalog.CreateSdl2Catalog()` view names exactly. The validator compares them order-insensitively against the stamp's `ParseViews` field. If a new pass is added in a future stage (e.g., tvOS, or per-backend granularity for SysWMinfo work), both this list and the catalog must move together; the validator is the safety net that catches drift.

Extend `build\_build\Data\Manifest\Models\VcpkgManifest.cs` to expose `BuiltinBaseline` from the root `builtin-baseline` JSON property. The validator must not reparse `vcpkg.json` through a separate ad-hoc model when the build-host data layer already owns the manifest contract.

Register it in `build\_build\Validation\ServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<IBindingGenerationCoherenceValidator, BindingGenerationCoherenceValidator>();
```

Wire it into `PreFlightCheckTask` with the same reporting/fatal pattern as existing validators.

- [ ] **Step 6: Provision SDL2 headers before CI PreFlight on the canonical Linux runner**

Update `.github\workflows\release.yml` so the PreFlight job runs on the canonical generation RID (Linux) inside the pinned `linux-builder` container and installs the matching SDL2 header tree before `PreFlightCheck`. The validator computes `HeaderFingerprint` against `vcpkg_installed/x64-linux-hybrid/include/SDL2/`, which is only meaningful when those headers exist locally.

Replace the preflight job runner + container:

```yaml
    runs-on: ubuntu-24.04
    container: ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest
```

Add the vcpkg setup action after `actions/setup-dotnet@v5` and before downloading artifacts:

```yaml
      - uses: ./.github/actions/vcpkg-setup
        with:
          platform-identity: ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest
          triplet: x64-linux-hybrid
          vcpkg-cache-path: .vcpkg-cache
          vcpkg-feature-flags: binarycaching
```

Pass the canonical RID into PreFlight:

```yaml
      - name: Run PreFlightCheck
        run: >
          dotnet ./cake-host/Build.dll
          --target PreFlightCheck
          --rid linux-x64
          --versions-file ${{ env.VERSIONS_FILE }}
```

The validator needs real headers to recompute `HeaderFingerprint` and `HeaderCount`; without this, CI would only prove the stamp file exists, which is fake safety wearing a tiny mustache.

- [ ] **Step 7: Add `tools.cs generate-bindings` Docker orchestration for local invocation**

The Cake target lives in the Cake host and runs on a Linux container only. Local invocation from a Windows or macOS dev machine flows through `tools.cs generate-bindings`, which provisions the container automatically. Add this subcommand to `tools.cs`:

```csharp
// tools.cs — add to the app.Configure(config => ...) block alongside SetupCommand / CiSimCommand:
config.AddCommand<GenerateBindingsCommand>("generate-bindings");

// Settings record:
public sealed class GenerateBindingsSettings : CommandSettings
{
    [CommandOption("-f|--family")]
    [Description("Family to regenerate. Stage 1 supports only 'sdl2-core'.")]
    [DefaultValue("sdl2-core")]
    public string Family { get; init; } = "sdl2-core";
}

// Command:
public sealed class GenerateBindingsCommand : AsyncCommand<GenerateBindingsSettings>
{
    private const string LinuxBuilderImage = "ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest";

    protected override async Task<int> ExecuteAsync(
        CommandContext ctx, GenerateBindingsSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var repoRoot = await Shared.ResolveRepoRootAsync();
        var hostRid = Shared.ResolveHostRid();
        var logDir = Shared.CreateLogDir(repoRoot, "generate-bindings");

        AnsiConsole.Write(new FigletText("tools").Color(Color.Yellow));
        AnsiConsole.MarkupLine($"[grey]Command:[/] [cyan]generate-bindings --family {Markup.Escape(settings.Family)}[/]");
        AnsiConsole.MarkupLine($"[grey]Host RID:[/] [cyan]{Markup.Escape(hostRid)}[/]");
        AnsiConsole.MarkupLine($"[grey]Repo:[/] [cyan]{Markup.Escape(repoRoot)}[/]");
        AnsiConsole.MarkupLine($"[grey]Image:[/] [cyan]{Markup.Escape(LinuxBuilderImage)}[/]");
        AnsiConsole.WriteLine();

        var cakeHostDir = Path.Combine(repoRoot, ".cake-host");
        var vcpkgCacheDir = Path.Combine(repoRoot, ".vcpkg-cache");

        // 1. Publish the Cake host (Release) into a side directory. Same artifact shape as release.yml.
        if (!await PublishCakeHostAsync(repoRoot, cakeHostDir, logDir))
        {
            return 1;
        }

        // 2. Ensure the host vcpkg cache directory exists so the container has something to mount.
        Directory.CreateDirectory(vcpkgCacheDir);

        // 3. Run the container with repo root + vcpkg cache mounted.
        //    .dockerignore excludes vcpkg_installed/, artifacts/, .vs/, bin/, obj/, .cake-host/ so the
        //    container's vcpkg install does not collide with host state.
        var dockerArgs = new[]
        {
            "run", "--rm", "--init",
            "-v", $"{repoRoot}:/workspace",
            "-v", $"{vcpkgCacheDir}:/workspace/.vcpkg-cache",
            "-w", "/workspace",
            "-e", "VCPKG_DEFAULT_BINARY_CACHE=/workspace/.vcpkg-cache",
            "-e", "VCPKG_FEATURE_FLAGS=binarycaching",
            LinuxBuilderImage,
            "bash", "-c",
            $"dotnet /workspace/.cake-host/Build.dll --target GenerateBindings --family {settings.Family} --rid linux-x64",
        };

        AnsiConsole.MarkupLine($"[grey]docker[/] [cyan]{Markup.Escape(string.Join(' ', dockerArgs))}[/]");
        var result = await Cli.Wrap("docker").WithArguments(dockerArgs).ExecuteAsync(cancellationToken);

        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLine($"[red]Docker exit {result.ExitCode}. Inspect logs under {Markup.Escape(logDir)}.[/]");
            return result.ExitCode;
        }

        AnsiConsole.MarkupLine($"[green]Bindings regenerated. Review with `git diff src/SDL2.Core/Generated`.[/]");
        return 0;
    }

    private static async Task<bool> PublishCakeHostAsync(string repoRoot, string cakeHostDir, string logDir)
    {
        if (Directory.Exists(cakeHostDir))
        {
            Directory.Delete(cakeHostDir, recursive: true);
        }

        AnsiConsole.MarkupLine("[grey]Publishing Cake host (Release) for container invocation...[/]");
        var args = new[]
        {
            "publish", "build/_build/Build.csproj",
            "-c", "Release",
            "-o", cakeHostDir,
        };

        var result = await Cli.Wrap("dotnet")
            .WithArguments(args)
            .WithWorkingDirectory(repoRoot)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLine($"[red]dotnet publish failed (exit {result.ExitCode}).[/]");
            return false;
        }

        return true;
    }
}
```

Extend `.dockerignore` at the repo root (create or update):

```text
artifacts/
vcpkg_installed/
.cake-host/
.vs/
**/bin/
**/obj/
```

This ensures the container's volume mount does not see host build artifacts, host vcpkg state, or IDE caches. The container does its own `vcpkg install` against the mounted `.vcpkg-cache` so the first cold run takes time and subsequent warm runs are fast.

**No host-OS fallback.** The `GenerateBindings` Cake target's Linux-canonical guard refuses to run on Windows or macOS. If a maintainer tries `dotnet run --file tools.cs -- build --target GenerateBindings ...` from a Windows shell, the target fails with the actionable diagnostic pointing at `tools.cs generate-bindings`. The supported local entry point is the Docker orchestration above.

- [ ] **Step 8: Run build-host tests**

Run:

```pwsh
dotnet test build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Expected: all build-host tests pass.

- [ ] **Step 9: Run GenerateBindings via tools.cs from any dev host**

Run:

```pwsh
dotnet run --file tools.cs -- generate-bindings --family sdl2-core
git --no-pager diff -- src\SDL2.Core\Generated
```

Expected: `tools.cs` publishes the Cake host, mounts repo root + vcpkg cache, invokes the `linux-builder` container which runs the `GenerateBindings` Cake target. Generated output matches the previously committed Stage 1 baseline (clean diff) — proves the determinism contract end-to-end across the host boundary.

- [ ] **Step 10: Commit checkpoint**

Proposed commit message:

```text
feat: orchestrate SDL2 binding generation through Cake host + tools.cs Docker entry
```

Before committing, present the summary and proposed message to Deniz and wait for approval.

---

## Task 9: Public API Snapshot for Generated SDL2.Core

**Files:**

- Modify: `build\_build.Tests\Build.Tests.csproj`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\ModuleInitializer.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Sdl2CorePublicApiTests.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\Snapshots\Sdl2CorePublicApiTests.ApprovePublicApi.verified.txt`

- [ ] **Step 1: Add snapshot packages through CLI**

Run:

```pwsh
dotnet add build\_build.Tests\Build.Tests.csproj package PublicApiGenerator
dotnet add build\_build.Tests\Build.Tests.csproj package Verify.TUnit
dotnet add build\_build.Tests\Build.Tests.csproj reference src\SDL2.Core\SDL2.Core.csproj
```

Expected: package references and central versions are updated by the CLI.

- [ ] **Step 2: Configure Verify**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\ModuleInitializer.cs`:

```csharp
using System.Runtime.CompilerServices;
using VerifyTests;

namespace Build.Tests.Unit.Targets.GenerateBindings;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifyBase.UseProjectRelativeDirectory("Snapshots");
    }
}
```

- [ ] **Step 3: Write public API approval test**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\Sdl2CorePublicApiTests.cs`:

```csharp
using PublicApiGenerator;

namespace Build.Tests.Unit.Targets.GenerateBindings;

public sealed class Sdl2CorePublicApiTests
{
    [Test]
    public Task ApprovePublicApi()
    {
        var api = typeof(SDL2.SDL).Assembly.GeneratePublicApi();
        return Verifier.Verify(api);
    }
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Expected: FAIL with a `.received.txt` snapshot because the verified baseline does not exist.

- [ ] **Step 4: Approve the initial snapshot**

Review the received snapshot. It must show:

```text
namespace SDL2
{
    public static unsafe partial class SDL
    {
        public readonly partial struct SDL_Window
        public static partial int SDL_Init(uint flags)
        public static partial void SDL_Quit()
    }
}
```

The real file will contain the full generated Core API. After review, copy the received file to:

```text
build\_build.Tests\Unit\Targets\GenerateBindings\Snapshots\Sdl2CorePublicApiTests.ApprovePublicApi.verified.txt
```

Delete any `*.received.*` files.

- [ ] **Step 5: Re-run snapshot tests**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Expected: PASS and no `*.received.*` files remain.

- [ ] **Step 6: Commit checkpoint**

Proposed commit message:

```text
test: snapshot SDL2 core public API
```

Before committing, present the summary and proposed message to Deniz and wait for approval.

---

## Task 10: Documentation, Final Validation, and Anti-Slop Gate

**Files:**

- Modify: `docs\binding-autogen\README.md`
- Modify: `docs\playbook\local-development.md`
- Modify: `docs\knowledge-base\release-guardrails.md`
- Modify: `docs\superpowers\specs\2026-05-14-binding-generator-architecture-design.md` only if implementation deliberately changes an approved spec detail

- [ ] **Step 1: Update binding-autogen README**

Add a Stage 1 section to `docs\binding-autogen\README.md`:

```markdown
## Stage 1 generator entry point

SDL2.Core binding generation now runs through the repository generator:

```pwsh
dotnet run --file tools.cs -- build --target GenerateBindings --family sdl2-core --rid win-x64
```

Generated source is committed under `src\SDL2.Core\Generated\`. Consumers never run the generator. The generated stamp at `src\SDL2.Core\Generated\.generated-stamp` records the generator/toolchain/header state used for the committed output.

```

- [ ] **Step 2: Update local development playbook**

Add a "Regenerating bindings" section to `docs\playbook\local-development.md`:

```markdown
## Regenerating SDL2.Core bindings

Stage 1 supports SDL2.Core generation:

```pwsh
dotnet run --file tools.cs -- build --target GenerateBindings --family sdl2-core --rid win-x64
dotnet build src\SDL2.Core\SDL2.Core.csproj -c Release
dotnet build src\SDL2.Image\SDL2.Image.csproj -c Release
dotnet build src\SDL2.Mixer\SDL2.Mixer.csproj -c Release
dotnet build src\SDL2.Ttf\SDL2.Ttf.csproj -c Release
dotnet build src\SDL2.Gfx\SDL2.Gfx.csproj -c Release
git --no-pager diff -- src\SDL2.Core\Generated
```

The generator reads `vcpkg_installed\<triplet>\include\SDL2` and writes committed source under `src\SDL2.Core\Generated\`. A clean second generation run should produce no diff. If the header tree is missing, run the existing local setup flow before generating.

```

- [ ] **Step 3: Update release guardrails**

Add a behavior-first row to `docs\knowledge-base\release-guardrails.md` in the PreFlight section:

```markdown
| Binding generation coherence | PreFlight | Verifies committed generated binding output has a deterministic `.generated-stamp` matching the Stage 1 SDL2.Core generator/toolchain contract. Fails before release work if SDL2.Core generated source is missing or stale. |
```

Do not assign a new G-number unless the existing guardrail catalog has already allocated one in adjacent docs.

- [ ] **Step 4: Run final managed verification**

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
dotnet build src\SDL2.Core\SDL2.Core.csproj -c Release
dotnet build src\SDL2.Image\SDL2.Image.csproj -c Release
dotnet build src\SDL2.Mixer\SDL2.Mixer.csproj -c Release
dotnet build src\SDL2.Ttf\SDL2.Ttf.csproj -c Release
dotnet build src\SDL2.Gfx\SDL2.Gfx.csproj -c Release
dotnet run --file tools.cs -- build --target GenerateBindings --family sdl2-core --rid win-x64
git --no-pager diff -- src\SDL2.Core\Generated
```

Expected: tests and all affected managed package builds pass; generator run produces no generated-source diff.

- [ ] **Step 5: Run package-first validation when native workspace is provisioned**

From the main provisioned checkout, run:

```pwsh
dotnet run --file tools.cs -- ci-sim
```

Expected: mini CI replay passes, including Pack and PackageConsumerSmoke. If the current checkout is not provisioned for native/vcpkg flows, do not initialize submodules or vcpkg in a throwaway worktree; run this from the main provisioned checkout before merge.

- [ ] **Step 6: Run Slopwatch**

Run:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

Expected: no warnings for disabled tests, broad suppressions, empty catch blocks, package version bypasses, or generated slop outside the excluded paths.

- [ ] **Step 7: Final git review**

Run:

```pwsh
git --no-pager status --short
git --no-pager diff --stat
git --no-pager diff -- build\_build\Targets\GenerateBindings build\_build\Validation\BindingGeneration build\_build\Build.csproj build\_build.Tests src\SDL2.Core Directory.Packages.props tools.cs .dockerignore tests\smoke-tests tests\Sandbox docs
```

Expected: diff contains only Stage 1 generator, generated SDL2.Core output, build-host orchestration/validator, tests, and directly related docs.

- [ ] **Step 8: Commit final checkpoint**

Proposed commit message:

```text
docs: document SDL2 core binding generation
```

Before committing, present the summary and proposed message to Deniz and wait for approval.

---

## Self-Review Checklist

- Spec coverage:
  - CppAst generator hosted in Cake build host under `build\_build\Targets\GenerateBindings\`: Tasks 1-6.
  - Full SDL2 `(OsCondition, BackendCondition[])` platform catalog (~7-8 entries) at function level: Task 3.
  - `SDL_syswm.h` typed-union layout **deferred to Stage 2**: classified as `deferred-to-stage-2` in `KnownUnsupportedDeclarationPolicy` (Task 4); `SDL_GetWindowWMInfo` emitted as function with opaque `SDL_SysWMinfo*` (Tasks 5-6); Stage 2 plan introduces the forward-declaration stub library and typed union layout.
  - Dual `[LibraryImport]` / `[DllImport]` output across the full TFM matrix: Tasks 5-6.
  - Typed readonly opaque handles and friendly overloads: Tasks 5-7.
  - Platform attribution from multi-pass parsing via **preprocessor-macro switching only** (no `--target`, no mingw-w64, no Apple SDK): Tasks 3, 5, and 6.
  - Satellite/shared core type topology and SDL2-CS helper compatibility: Tasks 4, 6, and 7.
  - Generated source under `src\SDL2.Core\Generated`: Task 6.
  - `.generated-stamp`: Tasks 2, 6, 8.
  - SDL2.Core removal from `external\sdl2-cs`: Task 7.
  - PreFlight coherence: Task 8.
  - **Linux-canonical host guard** in `GenerateBindingsTask` fails closed on non-`linux-x64` hosts: Task 8.
  - **`tools.cs generate-bindings` Docker orchestration** for local invocation from Windows/macOS dev hosts: Task 8.
  - Public API snapshot: Task 9.
  - Package-first smoke and real validation: Task 10.
  - Pack-stage exported-symbol validation: explicitly deferred by scope decision; Stage 1 preserves `EntryPoint` metadata for the later Stage 2 validator.
- Completeness scan:
  - No task relies on deferred hand-waving.
  - Known unsupported declarations are explicitly named and reasoned.
  - Any unmapped public declaration fails generation instead of falling back to `nint`.
- Type consistency:
  - Generator request type is `GenerateBindingsRequest`.
  - Pure orchestrator is `BindingGenerationPipeline` (Cake-free; in `Targets/GenerateBindings/`).
  - Cake-aware shell is `BindingGenerationRunner` (delegates to the pipeline, owns `ICakeContext`).
  - Cake task is `GenerateBindingsTask` (`[TaskName("GenerateBindings")]`, Linux-canonical host guard).
  - Parse view type is `PlatformParseView` (no `TargetSystem` field; preprocessor-macro-only).
  - Catalog type is `PlatformCatalog` with `(OsCondition, BackendCondition[])` tuple entries (~8 for Stage 1 SDL2.Core).
  - Stamp type is `GeneratedStamp` (deterministic, no wall-clock fields).
  - PreFlight validator is `BindingGenerationCoherenceValidator` under `build\_build\Validation\BindingGeneration\`.
  - `tools.cs generate-bindings` is the local invocation entry; Docker is a hard prerequisite.
