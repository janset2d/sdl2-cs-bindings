# SDL2 Core Binding Generator Stage 1 Implementation Plan

> **Status (2026-05-17):** ⚠ **SUPERSEDED.** Tasks 1-3.5 + Post-Implementation Review P0/P1 fixes already landed in commits `c00a2cb`, `0db0e31`, `9a5f59e`. Remaining Task 4/5/7/8/9 + PSTH-A/B/C/D/E/F/G + P2/P3 items are absorbed into [`../2026-05-17-binding-generator-unified-plan.md`](../2026-05-17-binding-generator-unified-plan.md) per the unified spec [`../../specs/2026-05-16-binding-generator-unified-design.md`](../../specs/2026-05-16-binding-generator-unified-design.md) §16 absorption map. P2.4 / P2.5 retraction notes (Task 8 forward-looking) and P2.6 cascade-lock (Task-visibility-convention slice) remain in force in this superseded plan but are also captured in the unified spec out-of-scope list. Kept in `superseded/` for historical reference + implementation-seed mining (Task 4 §Step 2 `CoreOwnedTypeMap.cs` + `KnownUnsupportedDeclarationPolicy.cs` factory snippets feed into Phase 3 of the unified plan).

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
>
> **Cake-native correction (2026-05-15):** Build-host code in this plan must use Cake-native path, filesystem, and JSON boundaries. In `build/_build`, use `Cake.Core.IO.DirectoryPath` / `FilePath`, `ICakeContext`, `CakeFileSystemExtensions`, and `CakeJsonExtensions`; do not introduce raw `System.IO` file/path APIs in target code. Unit and scenario tests use `FakeCakeWorld` / Cake `FakeFileSystem`, not real temp directories.

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

build\_build\Data\BindingGeneration\              (file-backed/tool-read binding contracts per ADR-003)
|-- GeneratedStamp.cs
`-- GeneratedStampRepository.cs                    (Cake-native .generated-stamp writer)

build\_build\Targets\GenerateBindings\HeaderSet\   (target-local SDL header input services)
|-- ResolvedHeaderSet.cs
|-- HeaderSetFingerprint.cs
|-- HeaderSetResolver.cs
`-- HeaderSetFingerprintCalculator.cs

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
|-- Parsing\PlatformCatalogTests.cs                 (asserts ~7-8 catalog entries, OS + backend macro groups)
|-- Parsing\CppAstParseRunnerTests.cs               (asserts preprocessor-macro switching only, no --target)
|-- Model\DeclarationMergePolicyTests.cs
|-- Model\TypeMappingPolicyTests.cs
|-- Model\KnownUnsupportedDeclarationPolicyTests.cs  (asserts SDL_SysWMinfo typed-union is `deferred-to-stage-2`)
|-- Emitting\CsCodeGeneratorTests.cs
|-- Snapshots\Sdl2CorePublicApiTests.ApprovePublicApi.verified.txt
`-- Sdl2CorePublicApiTests.cs

build\_build.Tests\Unit\Data\BindingGeneration\
`-- GeneratedStampRepositoryTests.cs

build\_build.Tests\Unit\Targets\GenerateBindings\HeaderSet\
|-- HeaderSetResolverTests.cs
`-- HeaderSetFingerprintCalculatorTests.cs

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
        // Stage 1 tasks register target-local generator collaborators here.
        // File-backed binding contracts are registered in AddData() under Data/BindingGeneration.
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
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
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
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings;

internal sealed record GenerateBindingsRequest(
    Sdl2CoreGenerationConfig Config,
    DirectoryPath VcpkgInstalledDirectory,
    string Triplet,
    DirectoryPath OutputDirectory);
```

Re-run the test:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
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

- Create: `build\_build\Data\BindingGeneration\GeneratedStamp.cs`
- Create: `build\_build\Data\BindingGeneration\GeneratedStampRepository.cs`
- Modify: `build\_build\Data\ServiceCollectionExtensions.cs`
- Create: `build\_build.Tests\Unit\Data\BindingGeneration\GeneratedStampRepositoryTests.cs`
- Create: `build\_build\Targets\GenerateBindings\HeaderSet\ResolvedHeaderSet.cs`
- Create: `build\_build\Targets\GenerateBindings\HeaderSet\HeaderSetFingerprint.cs`
- Create: `build\_build\Targets\GenerateBindings\HeaderSet\HeaderSetResolver.cs`
- Create: `build\_build\Targets\GenerateBindings\HeaderSet\HeaderSetFingerprintCalculator.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\HeaderSet\HeaderSetResolverTests.cs`
- Create: `build\_build.Tests\Unit\Targets\GenerateBindings\HeaderSet\HeaderSetFingerprintCalculatorTests.cs`

`.generated-stamp` is a persisted JSON contract and belongs in `Data/BindingGeneration` per ADR-003. Header-set discovery/fingerprinting reads the vcpkg-installed input tree for one target invocation; it is target behavior like Harvest's `ArtifactPlanner` / `BinaryClosureWalker`, so it lives under `Targets/GenerateBindings/HeaderSet` as non-static services.

- [ ] **Step 1: Write failing repository tests**

Create `build\_build.Tests\Unit\Targets\GenerateBindings\HeaderSet\HeaderSetResolverTests.cs`:

```csharp
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tests.Unit.Targets.GenerateBindings.HeaderSet;

public sealed class HeaderSetResolverTests
{
    [Test]
    public async Task ResolveSdl2CoreHeaders_Should_Return_Discovered_Header_Paths()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL.h", "/* umbrella */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_system.h", "/* system */")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/begin_code.h", "/* begin */");
        var resolver = new HeaderSetResolver(world.CakeContext);

        var result = resolver.ResolveSdl2CoreHeaders(world.RepoRoot.Combine("vcpkg_installed"), "x64-linux-hybrid");

        await Assert.That(result.Headers.Select(path => path.GetFilename().FullPath))
            .IsEquivalentTo(["SDL.h", "SDL_system.h", "begin_code.h"]);
    }

    [Test]
    public async Task ResolveSdl2CoreHeaders_Should_Throw_When_No_Core_Headers_Are_Found()
    {
        var world = FakeCakeWorld.CreateLinux();
        world.FileSystem.GetDirectory(world.RepoRoot.Combine("vcpkg_installed").Combine("x64-linux-hybrid").Combine("include").Combine("SDL2")).Create();
        var resolver = new HeaderSetResolver(world.CakeContext);

        var exception = Assert.Throws<CakeException>(() =>
            resolver.ResolveSdl2CoreHeaders(world.RepoRoot.Combine("vcpkg_installed"), "x64-linux-hybrid"));

        await Assert.That(exception.Message).Contains("No SDL2 headers");
    }

}
```

Create `build\_build.Tests\Unit\Targets\GenerateBindings\HeaderSet\HeaderSetFingerprintCalculatorTests.cs` with the line-ending normalization test. It uses `FakeCakeWorld`, `ResolvedHeaderSet`, and `HeaderSetFingerprintCalculator`.

Create `build\_build.Tests\Unit\Data\BindingGeneration\GeneratedStampRepositoryTests.cs`:

```csharp
using Build.Data.BindingGeneration;
using Build.Host.Cake;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Data.BindingGeneration;

public sealed class GeneratedStampRepositoryTests
{
    [Test]
    public async Task SaveAsync_Should_Write_Deterministic_Json_Without_Wall_Clock_Time()
    {
        var world = FakeCakeWorld.CreateLinux();
        var output = world.RepoRoot.Combine("Generated");
        var stampPath = output.CombineWithFilePath(".generated-stamp");
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
        var repository = new GeneratedStampRepository(world.CakeContext);

        await repository.SaveAsync(output, stamp);
        var first = await world.CakeContext.ReadAllTextAsync(stampPath);
        await repository.SaveAsync(output, stamp);
        var second = await world.CakeContext.ReadAllTextAsync(stampPath);
        var loaded = await repository.LoadAsync(output);

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(second).DoesNotContain("generated_at");
        await Assert.That(second).Contains("schema_version");
        await Assert.That(loaded).IsEqualTo(stamp);
    }
}
```

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because `Build.Data.BindingGeneration` and `Targets/GenerateBindings/HeaderSet` types do not exist.

- [ ] **Step 2: Implement target-local header-set services and Data-layer stamp contract**

Create `build\_build\Targets\GenerateBindings\HeaderSet\ResolvedHeaderSet.cs`:

```csharp
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.HeaderSet;

public sealed record ResolvedHeaderSet(DirectoryPath IncludeRoot, DirectoryPath Sdl2IncludeDirectory, IReadOnlyList<FilePath> Headers);
```

Create `build\_build\Targets\GenerateBindings\HeaderSet\HeaderSetFingerprint.cs`:

```csharp
namespace Build.Targets.GenerateBindings.HeaderSet;

public sealed record HeaderSetFingerprint(string Hash, int HeaderCount);
```

Create `build\_build\Targets\GenerateBindings\HeaderSet\HeaderSetResolver.cs` and `HeaderSetFingerprintCalculator.cs`. They are sealed target services using `ICakeContext`, not static helpers and not Data repositories. The calculator normalizes line endings to `Environment.NewLine`.

Create `build\_build\Data\BindingGeneration\GeneratedStamp.cs` and `GeneratedStampRepository.cs`. The stamp record carries `[JsonPropertyName]` attributes for snake_case fields; the repository has `LoadAsync` and `SaveAsync` and uses `CakeJsonExtensions` / `CakeFileSystemExtensions`.

Register the stamp repository in `build\_build\Data\ServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<IGeneratedStampRepository, GeneratedStampRepository>();
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
using Build.Data.BindingGeneration;
using Build.Targets.GenerateBindings.Parsing;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Parsing;

public sealed class CppAstParseRunnerTests
{
    [Test]
    public async Task Parse_Should_Return_Compilation_When_Header_Is_Valid()
    {
        var world = FakeCakeWorld.CreateLinux();
        var includeRoot = world.RepoRoot.Combine("include");
        var sdl2Root = includeRoot.Combine("SDL2");
        var header = sdl2Root.CombineWithFilePath("SDL_test.h");
        world.WithTextFile(header, "typedef int SDL_bool;\nextern int SDL_Init(unsigned int flags);\n");

        var parseView = new PlatformParseView("Neutral", PlatformConditionKind.Neutral, null, [], []);
        var runner = new CppAstParseRunner(new ParseDiagnosticFormatter());

        var result = runner.Parse(new ResolvedHeaderSet(includeRoot, sdl2Root, [header]), parseView);

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
    private const string DiagnosticLineSeparator = "\n";

    public string FormatErrors(string parseViewName, CppCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var messages = compilation.Diagnostics.Messages
            .Where(message => message.Type == CppLogMessageType.Error)
            .Select(message => "  " + message)
            .ToArray();

        return messages.Length == 0
            ? $"CppAst parse failed for {parseViewName} without diagnostics."
            : $"CppAst parse failed for {parseViewName}:{DiagnosticLineSeparator}{string.Join(DiagnosticLineSeparator, messages)}";
    }
}
```

Create `build\_build\Targets\GenerateBindings\Parsing\CppAstParseRunner.cs`:

```csharp
using CppAst;
using Build.Data.BindingGeneration;

namespace Build.Targets.GenerateBindings.Parsing;

internal sealed class CppAstParseRunner(ParseDiagnosticFormatter diagnosticFormatter)
{
    private readonly ParseDiagnosticFormatter _diagnosticFormatter = diagnosticFormatter ?? throw new ArgumentNullException(nameof(diagnosticFormatter));

    public CppAstParseResult Parse(ResolvedHeaderSet headerSet, PlatformParseView parseView)
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

## Task 3.5: Local Output Loop (precursor slice)

> See [`docs/superpowers/plans/2026-05-15-binding-generator-local-output-loop.md`](2026-05-15-binding-generator-local-output-loop.md) for the full implementation plan and [`docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md`](../specs/2026-05-15-binding-generator-local-output-loop-design.md) for the design rationale.

This precursor slice establishes the Docker-based local generation loop (Cake `GenerateBindings` target + `tools.cs generate-bindings` subcommand + derived `binding-generator.Dockerfile`) so subsequent model + emitter tasks (4-6) iterate against real CppAst-output artifacts under `artifacts/generated-bindings-preview/sdl2-core/`. Output is gitignored at this stage; production-location flag-flip to `src/SDL2.<Family>/Generated/` lands at Task 7.

The slice introduces temporary scaffolding explicitly marked for retirement:

- `IPathService.GenerateBindingsPreviewRoot` + `GetGenerateBindingsPreviewFamilyRoot(string family)` — replaced by manifest family-id aware path API at Task 7.
- `PreviewBindingModel` + `CppAstToPreviewModel` translator — replaced by real binding model at Task 4.
- `PreviewEmitter` — replaced by per-category emitters at Task 5.

The slice also adds defensive infrastructure that stays beyond Stage 1:

- Maintenance playbook §"Trio Pinning" version table and `BindingGenerationRunner` libclang version assertion.
- `.dockerignore` repo-root entry (filters host build state from container build context).
- `tools.cs generate-bindings` Spectre.Console.Cli subcommand.

**2026-05-16 update — Stage 1 Task 3.5 production findings.** Container smoke surfaced refinements not anticipated in the 2026-05-15 plan: per-header `CppParser.ParseFile` inner loop (outer per-view loop alone insufficient), 5 mandatory + 3 defensive platform-stub headers under `SyntheticHeaders/`, `SDL_DISABLE_*MMINTRIN_H` family defines, `-fdeclspec` flag, `-U__has_builtin` flag with corrected mechanism, `HeaderSetResolver.ExcludedHeaders` covering umbrella / scaffolding / satellite-umbrella / satellite-prefix / GL-convenience-wrapper / GL-sub-header / test-scaffolding categories, MacOS view `MAC_OS_X_VERSION_MIN_REQUIRED=1070` + iOS view `TARGET_OS_IPHONE=1`. Full friction log in [`../../binding-autogen/research/binding-autogen-spike-findings.md`](../../binding-autogen/research/binding-autogen-spike-findings.md) §11. Sub-task 11.5 in the precursor plan addresses the remaining AST inline filter + dynapi cross-check validator implementation — Task 4 below has hard dependencies on those two deliverables landing first.

---

## Task 4: Binding Model, Merge Policy, and Fail-Closed Validators

> **Prerequisites (added 2026-05-16):** Task 11.5 of [`2026-05-15-binding-generator-local-output-loop.md`](2026-05-15-binding-generator-local-output-loop.md) — AST inline filter (`CppFunctionFlags.Inline` skip per Silk.NET pattern) + `-U__has_builtin` restoration + Dynapi cross-check validator. These three deliverables remove ~16 SDL_FORCE_INLINE false-positives from the AST collection and establish the public-API ground-truth oracle that Task 4's `DeclarationCollector` / merge policy / fail-closed validators build on. Without them, Task 4's model carries leaks that fail at consumer runtime (`EntryPointNotFoundException`).
>
> **Cross-stage validator integration.** Task 11.5.C lands the dynapi cross-check validator directly under `build/_build/Validation/BindingGeneration/BindingPublicApiCoherenceValidator.cs` (root-level placement, per existing build-host convention — `HybridStaticOverlayValidator`, `BindingVcpkgCoherenceValidator`, `BindingSymbolExistenceValidator` all sit under `build/_build/Validation/`). The parsed `DynapiManifest` record lives under `build/_build/Data/BindingGeneration/` per ADR-003 contract-centric data layer, alongside its `IDynapiManifestRepository` / `DynapiManifestRepository` resolver (vcpkg buildtree primary, WebFetch fallback). Stage 2 PreFlight and Stage 2 Pack stages reuse the same validator + manifest record with different `SeverityProfile` values — Task 4 picks up the PreFlight wiring; Stage 2 Pack stage `BindingSymbolExistenceValidator` uses the same manifest as the expected-set input for its per-RID binary checks.

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
- Create: `build\_build\Targets\GenerateBindings\Validation\*.cs` (the dynapi cross-check validator landed in Task 11.5 lives here; Task 4 adds the additional fail-closed validators per its merge-policy work)
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
        const string syswmDeferralReason =
            "deferred-to-stage-2: SDL_syswm typed-union layout requires the platform-handle forward-declaration stub library (HWND/HDC/Display*/Window/etc.) and `[StructLayout(LayoutKind.Explicit, Size = 64)]` emission. Stage 1 emits SDL_GetWindowWMInfo with an opaque SDL_SysWMinfo* parameter; the typed union lands in Stage 2.";
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
            ["SDL_snprintf"] = variadicReason,
            // SysWM typed-union deferral (forward-declared by Stage 1 §"Component layout" + §2098 + §2127).
            // Seeded from the pre-Task-4 `Sdl2CoreGenerationConfig.DeferredDeclarations` list that
            // was retired in the P2-α slice (2026-05-16) — the prior Stage-1 config-field location
            // was a placeholder; this policy is the proper home per the Model/ layout.
            ["SDL_SysWMinfo"] = syswmDeferralReason,
            ["SDL_SysWMmsg"] = syswmDeferralReason,
        });
    }

    public string? TryGetReason(string declarationName)
    {
        return _reasons.TryGetValue(declarationName, out var reason) ? reason : null;
    }
}
```

Create `build\_build\Targets\GenerateBindings\Model\CoreOwnedTypeMap.cs`. This is the proper home for the SDL2-Core owned-identifier prefix gating that was held in `Sdl2CoreGenerationConfig.OwnedPrefixes` as a placeholder before being retired in the P2-α slice (2026-05-16). The prefixes are: `SDL_` (functions / types / general macros), `SDLK_` (keycode constants like `SDLK_RETURN`), `SDL_HINT_` (hint-name constants like `SDL_HINT_RENDER_DRIVER`), `SDL_INIT_` (init-flag constants like `SDL_INIT_VIDEO`). Any identifier whose name starts with one of these belongs to SDL2-Core's surface; satellites (sdl2-image / sdl2-mixer / sdl2-ttf / sdl2-net / sdl2-gfx) own their own prefixes (`IMG_`, `Mix_`, `TTF_`, `SDLNet_`, `SDL2_gfx*`) and the satellite `CoreOwnedTypeMap` equivalents will be introduced in their respective per-family slices. `OrdinalIgnoreCase` because preprocessor macros and identifiers can mix case (e.g. `SDLK_a`).

```csharp
namespace Build.Targets.GenerateBindings.Model;

internal sealed class CoreOwnedTypeMap
{
    private readonly IReadOnlyList<string> _ownedPrefixes;

    private CoreOwnedTypeMap(IReadOnlyList<string> ownedPrefixes)
    {
        _ownedPrefixes = ownedPrefixes;
    }

    public static CoreOwnedTypeMap CreateSdl2Core()
    {
        return new CoreOwnedTypeMap(
        [
            "SDL_",
            "SDLK_",
            "SDL_HINT_",
            "SDL_INIT_",
        ]);
    }

    public bool IsOwned(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return _ownedPrefixes.Any(prefix => identifier.StartsWith(prefix, StringComparison.Ordinal));
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

> **Marshalling shape parity vs. SDL2-CS (added 2026-05-16).** Stage 1 Task 3.5's placeholder `PreviewEmitter` produces raw-pointer-only command signatures (e.g., `internal static extern int SDL_RenderGeometry(IntPtr renderer, IntPtr vertices, int num_vertices, int* indices, int num_indices)`). SDL2-CS — the existing UX baseline — emits typed managed-array overloads with `[In] SDL_Vertex[] vertices, [In] int[] indices`. The wire-level marshalling is equivalent, but SDL2-CS's shape lets callers pass C# arrays without manual `GCHandle.Alloc` pinning. Task 5's emitter set MUST include the friendly-overload layer per ADR-004 §3 ("emit friendly overloads for UTF-8 strings, spans, `out`, and `ref` shapes alongside raw P/Invoke entry points") to close that UX gap. Concrete acceptance criterion: every `T*` parameter in the raw P/Invoke layer that takes a buffer + length pair MUST emit a corresponding `[In] T[]` overload (or `ReadOnlySpan<T>` per Rule 6) in the friendly-overload layer. Verification: spot-check `SDL_RenderGeometry` against SDL2-CS's `SDL2.cs:3209-3216` signature; document any signature parity gaps as ADR or feasibility-doc decisions before locking the emitter shape.

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
- Create: `build\_build\Targets\GenerateBindings\BindingGenerationError.cs`
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
        var world = FakeCakeWorld.CreateLinux()
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL.h", """
                typedef unsigned int Uint32;
                typedef int SDL_bool;
                typedef struct SDL_Window SDL_Window;
                extern int SDL_Init(Uint32 flags);
                extern void SDL_Quit(void);
                extern SDL_Window* SDL_CreateWindow(const char *title, int x, int y, int w, int h, Uint32 flags);
                extern void SDL_DestroyWindow(SDL_Window *window);
                """);

        var output = world.RepoRoot.Combine("Generated");
        var request = new GenerateBindingsRequest(
            Config: Sdl2CoreGenerationConfig.Default,
            VcpkgInstalledDirectory: world.RepoRoot.Combine("vcpkg_installed"),
            Triplet: "x64-linux-hybrid",
            OutputDirectory: output);

        // Build the coordinator from real collaborators. It still receives world.CakeContext so
        // all path, filesystem, and JSON work goes through Cake-native helpers.
        var runner = TestRunnerFactory.Create();
        await runner.RunAsync(world.CakeContext, request);

        await Assert.That(world.FileExists("Generated/Commands.g.cs")).IsTrue();
        await Assert.That(world.FileExists("Generated/Handles.g.cs")).IsTrue();
        await Assert.That(world.FileExists("Generated/.generated-stamp")).IsTrue();
        await Assert.That(world.ReadAllText("Generated/Commands.g.cs")).Contains("SDL_Init");
    }
}
```

`TestRunnerFactory.Create()` is a small fixture helper that news up collaborators directly when no DI container is needed. Tests still pass `FakeCakeWorld.CakeContext`; do not introduce a separate fake Cake context helper.

Run:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
```

Expected: FAIL because the full in-process generation pipeline is not yet implemented end-to-end (individual collaborators exist from Tasks 2-5 but their composition isn't wired through `BindingGenerationRunner` yet).

- [ ] **Step 2: Implement generation coordinator**

Create `build\_build\Targets\GenerateBindings\BindingGenerationError.cs` and use the existing `Build.Results.Result<T,TError>` pattern. Do not introduce a custom success/failure shape for generation.

```csharp
namespace Build.Targets.GenerateBindings;

internal sealed record BindingGenerationError(string Message, Exception? Exception = null);
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

Create `build\_build\Targets\GenerateBindings\BindingGenerationCoordinator.cs` — the target-local coordinator for parse/merge/emit. It receives `ICakeContext` because header reads, generated-file writes, stamp writes, and repository JSON reads are build-host IO and must stay Cake-native. There is no separate console app and no `Cli/` subdirectory.

```csharp
using Build.Host.Cake;
using Build.Data.BindingGeneration;
using Build.Results;
using Build.Targets.GenerateBindings.Emitting;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;
using Cake.Common.IO;
using Cake.Core;

namespace Build.Targets.GenerateBindings;

internal sealed class BindingGenerationCoordinator(
    HeaderSetResolver headerSetResolver,
    HeaderSetFingerprintCalculator headerSetFingerprintCalculator,
    IGeneratedStampRepository generatedStampRepository,
    CppAstParseRunner parseRunner,
    PlatformCatalog platformCatalog,
    DeclarationCollector declarationCollector,
    DeclarationMergePolicy mergePolicy,
    CsCodeGenerator codeGenerator,
    GeneratedStampFactory stampFactory)
{
    public async Task<Result<GeneratedFileSet, BindingGenerationError>> RunAsync(ICakeContext context, GenerateBindingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var headerSet = headerSetResolver.ResolveSdl2CoreHeaders(request.VcpkgInstalledDirectory, request.Triplet);
        var fingerprint = await headerSetFingerprintCalculator.ComputeAsync(headerSet).ConfigureAwait(false);

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

        await WriteFilesAsync(context, request.OutputDirectory, generated).ConfigureAwait(false);
        var stamp = await stampFactory.CreateAsync(context, request, fingerprint, catalog.ParseViews).ConfigureAwait(false);
        await generatedStampRepository.SaveAsync(request.OutputDirectory, stamp).ConfigureAwait(false);

        return Result<GeneratedFileSet, BindingGenerationError>.Success(generated);
    }

    private static async Task WriteFilesAsync(ICakeContext context, DirectoryPath outputDirectory, GeneratedFileSet generated)
    {
        if (context.DirectoryExists(outputDirectory))
        {
            context.DeleteDirectory(outputDirectory, new DeleteDirectorySettings { Recursive = true, Force = true });
        }

        foreach (var file in generated.Files)
        {
            var path = outputDirectory.CombineWithFilePath(file.Key);
            await context.WriteAllTextAsync(path, file.Value).ConfigureAwait(false);
        }
    }
}
```

The coordinator is target-local because it is not yet shared with SDL3, but it is not a Cake-free island: generated output and stamp state are build-host IO and therefore go through `ICakeContext` / Cake helper extensions. Pure model and emitter policies remain Cake-free beneath it.

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
                SourceHeader: GetSourceHeaderName(function.SourceFile!),
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

    private static string GetSourceHeaderName(string sourceFile)
    {
        var normalized = sourceFile.Replace('\\', '/');
        var separator = normalized.LastIndexOf('/');
        return separator < 0 ? normalized : normalized[(separator + 1)..];
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
using Build.Data.BindingGeneration;
using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Emitting;

namespace Build.Targets.GenerateBindings;

internal sealed class BindingGenerationRunner(
    HeaderSetResolver headerSetResolver,
    HeaderSetFingerprintCalculator headerSetFingerprintCalculator,
    IGeneratedStampRepository generatedStampRepository,
    CppAstParseRunner parseRunner,
    PlatformCatalog platformCatalog,
    DeclarationCollector declarationCollector,
    DeclarationMergePolicy mergePolicy,
    CsCodeGenerator codeGenerator,
    GeneratedStampFactory stampFactory)
{
    public async Task RunAsync(ICakeContext context, GenerateBindingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        // 1. Resolve canonical headers (vcpkg-installed SDL include tree under request.Triplet).
        var headerSet = headerSetResolver.ResolveSdl2CoreHeaders(request.VcpkgInstalledDirectory, request.Triplet);
        var fingerprint = await headerSetFingerprintCalculator.ComputeAsync(headerSet, context.CancellationToken).ConfigureAwait(false);

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
        var generated = codeGenerator.Emit(bindingModel, request.Config);
        await GeneratedFileSetWriter.WriteAsync(context, request.OutputDirectory, generated, context.CancellationToken).ConfigureAwait(false);

        // 6. Write the deterministic .generated-stamp.
        var stamp = await stampFactory.CreateAsync(context, request, fingerprint, parseViews).ConfigureAwait(false);
        await generatedStampRepository.SaveAsync(request.OutputDirectory, stamp, context.CancellationToken).ConfigureAwait(false);

        context.Log.Information(
            "GenerateBindings: emitted {0} declarations across {1} parse views for {2}.",
            bindingModel.DeclarationCount,
            parseViews.Count,
            request.Config.Family);
    }
}
```

The target-local generator collaborators (`CppAstParseRunner`, `PlatformCatalog`, `DeclarationCollector`, `DeclarationMergePolicy`, `CsCodeGenerator`) are registered through `AddGenerateBindings()`. Header and stamp file contracts are Data-layer repositories from `AddData()` per ADR-003. The runner is the Cake-aware shell that ties them together — see ADR-002 §2.2 and ADR-003 §2.3.

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
            VcpkgInstalledDirectory: context.Paths.GetVcpkgInstalledDir,
            Triplet: "x64-linux-hybrid", // canonical Stage 1 triplet
            OutputDirectory: context.Paths.RepoRoot.Combine("src").Combine("SDL2.Core").Combine("Generated"));

        await _runner.RunAsync(context, request);
        return Task.CompletedTask;
    }
}
```

Update `build\_build\Targets\GenerateBindings\ServiceCollectionExtensions.cs` (the placeholder from Task 1 is fleshed out here once all collaborators exist):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Emitting;

namespace Build.Targets.GenerateBindings;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGenerateBindings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Target-local collaborators. File-backed binding contracts come from AddData().
        services.AddSingleton<ParseDiagnosticFormatter>();
        services.AddSingleton<CppAstParseRunner>();
        services.AddSingleton<PlatformCatalog>();
        services.AddSingleton<DeclarationCollector>();
        services.AddSingleton<DeclarationMergePolicy>();
        services.AddSingleton<CoreOwnedTypeMap>();
        services.AddSingleton<KnownUnsupportedDeclarationPolicy>();
        services.AddSingleton<TypeMappingPolicy>();
        services.AddSingleton<CsCodeGenerator>();

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
        .WithTextFile(VersionsFilePath, FixtureLoader.Load("Versions/versions-valid.json"));
    world.WithTextFile("src/SDL2.Core/Generated/.generated-stamp", world.CakeContext.SerializeJson(stamp));

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

        var cakeHostDir = Shared.RepoPath(repoRoot, ".cake-host");
        var vcpkgCacheDir = Shared.RepoPath(repoRoot, ".vcpkg-cache");

        // 1. Publish the Cake host (Release) into a side directory. Same artifact shape as release.yml.
        if (!await PublishCakeHostAsync(repoRoot, cakeHostDir, logDir))
        {
            return 1;
        }

        // 2. Ensure the host vcpkg cache directory exists so the container has something to mount.
        Shared.EnsureDirectoryExists(vcpkgCacheDir);

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

````markdown
## Stage 1 generator entry point

SDL2.Core binding generation now runs through the repository generator:

```pwsh
dotnet run --file tools.cs -- build --target GenerateBindings --family sdl2-core --rid win-x64
```

Generated source is committed under `src\SDL2.Core\Generated\`. Consumers never run the generator. The generated stamp at `src\SDL2.Core\Generated\.generated-stamp` records the generator/toolchain/header state used for the committed output.

````

- [ ] **Step 2: Update local development playbook**

Add a "Regenerating bindings" section to `docs\playbook\local-development.md`:

````markdown
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

````

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

## Pre-Stage-2 Transition Hardening

> **Context (2026-05-16):** Stage 1 Task 3.5's precursor slice landed working output (848 Neutral functions + 51 platform-only delta, dynapi-validated), but the implementation carries Stage 1 / "preview" naming and one-family hardcoding that does not survive into Stage 2's multi-family generation scope. These items are **transitional cleanup mandatory before Stage 2 Task 4 onwards (real binding model + per-category emitters + satellite sweep) starts** — they are not "Stage 2 work," they are "the prerequisites Stage 2 builds on." Each item is recorded with enough scope context that a future agent (or Deniz himself, after a long pause) can pick it up cold. Code-side comments **must not reference these items** — implementation comments stay self-explanatory per AGENTS.md §"Code comments must be self-contained"; cross-doc references live in PR descriptions / commit messages / this plan.

### PSTH-A: Preview / Stage 1 naming purge

The Stage 1 Task 3.5 implementation introduced "Preview" and "Stage 1 scratch" naming throughout the binding generator surface. These names made sense as transient scaffolding; they make zero sense for a feature-frozen Stage 2 surface. Rename + retire:

Production code:

- `PreviewBindingModel` → `BindingModel`
- `PreviewParseView` → `BindingParseView` (or whatever the real Task 4 model nomenclature settles on — this depends on the cross-family design)
- `PreviewFunction` → `BindingFunction` (or absorbed into the real model's `BindingDeclaration` polymorphism)
- `PreviewParameter` → `BindingParameter`
- `PreviewParseViewReport` → `BindingParseViewReport` (or whatever Task 4/5 emits)
- `PreviewEmitter` → broken into `CsCommandsEmitter` / `CsHandlesEmitter` / `CsStructsEmitter` / etc. per Task 5 of this plan
- `CppAstToPreviewModel` → `CppAstToBindingModel` (or absorbed into the Task 5 model)
- `GenerateBindingsPreviewRoot` / `GetGenerateBindingsPreviewFamilyRoot` (in `IPathService`) → renamed `BindingGeneratorOutputRoot` / `GetBindingGeneratorOutputRoot(string family)` (or absorbed into Task 7's production-location flag-flip)

Test code:

- `PreviewBindingModelData` → `BindingModelData` (or per-test fixture)
- `PreviewEmitterTests` / `PreviewParseViewReportTests` → renamed in lockstep with the emitter rename

Emitted artifacts (consumer-visible — most important to clean before Task 7's production flag-flip):

- `Sdl2Preview_<view>` emitted class prefix → `Sdl2Bindings_<view>` (or whatever Task 5/7 naming settles)
- `Janset.Sdl2.Preview` emitted namespace → `Janset.Sdl2` (post-Task 7 production-location flag-flip)
- `PreviewEmitter` emits `// Stage 1 binding-autogen preview output. Placeholder shape; replaced by real emitter at Stage 1 Task 5.` into every generated `.g.cs` file → strip (internal process commentary should not bleed into consumer-visible artifacts)
- `unsafe partial` modifiers on emitted classes without any unsafe operations or partial extensions → drop until/unless used

Also retire Stage-1-only comments referencing "placeholder shape," "scratch output," "Task 5 replaces this," etc. Once Task 5 lands, those references are stale lore.

### PSTH-B: Family-based binding generation architecture

`GenerateBindingsTask` is currently hardcoded to `sdl2-core`. Stage 2 introduces SDL2_image, SDL2_mixer, SDL2_ttf, SDL2_gfx, SDL2_net binding families; each family has different prefix ownership (IMG_*, Mix_*, TTF_*, gfx*, SDLNet_*) and a different header set within the same vcpkg `SDL2/` include directory. The current implementation cannot grow to that shape without a refactor.

Target architecture (parallel to existing Cake task family-scoping pattern in `Harvest`, `Package`, `NativeSmoke`, etc.):

- `GenerateBindingsTask` takes a `--family <name>` CLI argument (like `Harvest --library <name>`).
- Per-family config types: `Sdl2CoreGenerationConfig` (already exists), `Sdl2ImageGenerationConfig`, `Sdl2MixerGenerationConfig`, ... — each declaring own `OwnedPrefixes`, header-set rules, deferred declarations, **and validator strategy** (see Stage 2 validator note below).
- A family-resolver service maps the CLI argument to the right config (mirrors `ManifestFamilyConfig` lookup pattern from existing code).
- `HeaderSetResolver.ResolveSdl2CoreHeaders` → `ResolveHeadersForFamily(familyConfig)`; the resolver consults the family config for inclusion / exclusion rules instead of hardcoding.

**Validator strategy is per-family.** Stage 1's `IDynapiManifestRepository` + `IBindingPublicApiCoherenceValidator` pair is **SDL2-only** — SDL2's dynapi system (`src/dynapi/SDL2.exports`, redistributed via the Janset SDL2 overlay port to `share/sdl2/dynapi/`) has no equivalent in SDL2_image / SDL2_mixer / SDL2_ttf / SDL2_gfx / SDL2_net. Satellites use `extern DECLSPEC` header declarations directly as the export ground truth (no separate textual manifest). Stage 2 must therefore make the validator surface configurable on the family config — `Sdl2CoreGenerationConfig.Validator = DynapiCrossCheck(SDL2.exports path)`; `Sdl2ImageGenerationConfig.Validator = HeaderDerivedExports` (or analogous), with a third option `HarvestedBinarySymbols` available for Stage 2 Pack-stage cross-check via the existing `BinaryClosureWalker` (dumpbin / nm / otool). Captured in `docs/playbook/binding-generator-maintenance.md` §Dynapi Manifest Cross-Check scope note.

### PSTH-C: Header set rules — per-family, not global

`HeaderSetResolver.ExcludedHeaders` currently hardcodes "satellite umbrellas" (`SDL_image.h`, `SDL_mixer.h`, `SDL_net.h`, `SDL_ttf.h`) and the `SDL2_*` prefix exclusion. **This is dangerously coupled to Stage 1's sdl2-core scope.** When the satellite generation slice arrives (PSTH-B), it MUST include these headers — and an sdl2-image generation pass MUST exclude SDL2.Core headers (SDL_video.h, SDL_render.h, etc., which sdl2-image consumes but does not own).

Refactor target: the resolver receives a header-selection policy from the family config:

- Sdl2Core: include `SDL_*.h` except `{SDL.h, begin_code.h, close_code.h, SDL_image.h, SDL_mixer.h, SDL_net.h, SDL_ttf.h, SDL2_*, SDL_opengl*, SDL_egl.h, SDL_test*}`.
- Sdl2Image: include `SDL_image.h` only.
- Sdl2Mixer: include `SDL_mixer.h` only.
- Sdl2Gfx: include `SDL2_*.h` (gfx family) only.
- Each satellite's emitter filters by `OwnedPrefixes` so SDL_-prefixed signatures referenced from satellite headers map to core types (cross-csproj reference, never duplicate).

### PSTH-D: Manifest centralization

`Sdl2CoreGenerationConfig.OwnedPrefixes` + `DeferredDeclarations` + (eventually) per-family header rules live in code today. Strategy brief / ADR-003 contract-centric data layer suggests declarative config in `build/manifest.json` `package_families[]`. **Evaluate before PSTH-B implementation:**

- Pro: one source of truth (`manifest.json` already drives runtimes, package families, library manifests, system exclusions); declarative; visible to ops without C# read.
- Pro: cross-cutting validators (PreFlight, BindingPublicApi) read directly from manifest without C# round-trip.
- Con: schema bump v2.2 with binding-generation fields; needs schema migration discipline.
- Con: comments / explanations sit better in C# config code than JSON.

Recommended outcome: hybrid — `manifest.json` carries identity (family name, owned prefixes, allowed header globs); C# config record carries logic + documentation (deferred declarations rationale, type mapping policy refs). The C# record is constructed from manifest-read at task entry.

### PSTH-E: ExcludedHeaders configurable (natural consequence of C+D)

The current `HeaderSetResolver.ExcludedHeaders` static `HashSet<string>` is global. After C+D it becomes family-scoped (`familyConfig.HeaderExclusions`). The static set goes away; the resolver becomes a policy executor.

### PSTH-F: Code-side doc-reference temizliği

The Stage 1 Task 3.5 implementation added comments that link to documents (`see spec §11.2`, `see docs/binding-autogen/research/...`). **AGENTS.md "Code comments must be self-contained" rule was violated.** Audit and clean:

- `CppAstParseRunner.cs` — comments reference spec sections and dynapi research. Rewrite to explain the local logic without cross-doc references.
- `HeaderSetResolver.cs` — comments reference spec / strategy brief decisions. Same fix.
- `DynapiManifestRepository.cs` — comments reference ClangSharp issue #414 + ppy/SDL3-CS. Same fix — explain the local mechanism, not the research provenance.
- Synthetic header stubs — comments reference spike-findings / playbook. Same fix.
- `binding-generator.Dockerfile` — peer references in CPATH comment block. Same fix.

Cross-doc references belong to PR description, commit message, ADR, or this plan. **Code reads on its own.**

### PSTH-G: AGENTS.md update post-Stage-2

After PSTH-A through PSTH-F land + Stage 2 satellite sweep ships, AGENTS.md §"Settled Strategic Decisions" gains a row: "Binding generation = family-scoped + manifest-driven." The "Binding autogen replaces SDL2-CS" + "`external/sdl2-cs` is transitional" rows collapse into one acknowledging completion. Phase 6 task.

### PSTH-H: OverlayPortVersionCoherenceValidator (PreFlight guardrail)

Today every overlay port under `vcpkg-overlay-ports/<port>/` carries its own `vcpkg.json` with a hardcoded `"version": "X.Y.Z"` field. The upstream port under `external/vcpkg/ports/<port>/vcpkg.json` (at the submodule's pinned commit) has its own version. **There is no automated check that the two match.** A vcpkg submodule bump can advance the upstream port to a new version without anyone noticing the overlay still pins the previous one — vcpkg's behaviour is to silently use the overlay's version when an overlay is active.

This is a real drift risk for the existing overlays today (`sdl2-mixer`, `sdl2-gfx`, `mpg123`) and any future overlay we add. The overlay README's "Maintenance Rules" section prescribes manual diffing on baseline bumps — but discipline doesn't catch missed steps.

**Target validator:**

- New `OverlayPortVersionCoherenceValidator` under `build/_build/Validation/Vcpkg/` (or fold into an existing `Vcpkg/` validation cohort if one materializes).
- Reads every `vcpkg-overlay-ports/<port>/vcpkg.json` `"version"` (and `"port-version"` if present).
- Reads each matching `external/vcpkg/ports/<port>/vcpkg.json` (the upstream at the current submodule HEAD).
- Cross-checks `version` + `port-version` fields. Mismatch → `ValidationCheck` with `ValidationSeverity.Error`, message lists overlay version vs upstream version vs upstream ref (commit + port name) + actionable next step ("re-sync overlay against upstream — see `vcpkg-overlay-ports/README.md` §Maintenance Rules").
- Wired into `PreFlightCheckTask` via `AddValidators()` registration + `PreflightReporter` row.
- New guardrail ID: **G60** (next free slot — G59 was already taken by `ManifestFamilyNameInvariantValidator`).

**Sequencing:** independent of PSTH-A through G. Can land any time. Recommended as a small focused slice with its own commit.

### PSTH-I: vcpkg-setup action.yml multi-path cache for binding regen

**Landed 2026-05-16.** `.github/actions/vcpkg-setup/action.yml` cache step extended to multi-path: existing binary cache directory + `external/vcpkg/buildtrees/sdl2/src`. Same cache key (`hashFiles('vcpkg.json', 'vcpkg-overlay-triplets/**', 'vcpkg-overlay-ports/**')-${vcpkg_commit}`) already invalidates on relevant changes. Unblocks PSTH-J workflow.

---

`.github/actions/vcpkg-setup/action.yml` caches only `${{ inputs.vcpkg-cache-path }}` (vcpkg's binary cache). Binary cache restoration unpacks compiled artifacts into `installed/<triplet>/<port>/` but **does not re-extract source** into `buildtrees/<port>/src/` — vcpkg behaviour, confirmed against [vcpkg docs](https://learn.microsoft.com/en-us/vcpkg/users/binarycaching). The Stage 1 dynapi cross-check validator reads `SDL2.exports` from `external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports`. On a binary-cache hit the file is unreachable in CI.

**Target change to `vcpkg-setup/action.yml`:**

```yaml
- uses: actions/cache@v5
  with:
    path: |
      ${{ github.workspace }}/${{ inputs.vcpkg-cache-path }}
      ${{ github.workspace }}/external/vcpkg/buildtrees/sdl2/src
    key: vcpkg-bin-${{ ... }}-${{ inputs.triplet }}-${{ hashFiles('vcpkg.json', 'vcpkg-overlay-triplets/**', 'vcpkg-overlay-ports/**') }}-${{ steps.vcpkg_commit.outputs.commit }}
```

Existing cache key already invalidates on `vcpkg.json` (which carries `builtin-baseline`) + overlay tree changes + vcpkg submodule commit — same key applies to buildtrees, no key changes needed.

Trade-off: cache size grows by ~80 MB (SDL2 source tree). Trivial against GH Actions' multi-GB cache budget.

**Sequencing:** prerequisite for PSTH-J. Can land independently before the workflow is wired up.

### PSTH-J: regenerate-bindings.yml workflow

**Landed 2026-05-16.** `.github/workflows/regenerate-bindings.yml` ships two jobs: `build-cake-host` (mirrors `release.yml`'s host build) + `regenerate` (container-job using PSTH-I's multi-path vcpkg-setup action). Trigger: `workflow_dispatch` only. Output: `generated-bindings-preview` artifact (manual review until Task 7 production-location flag-flip — auto-PR via peter-evans is deferred because the gitignored preview path has no committed home for a diff PR to apply against). Original target architecture below preserved for reference:

New GitHub Actions workflow that runs the Cake `GenerateBindings` target on a fresh runner via the same container-job pattern as `release.yml`'s `harvest` job:

```yaml
jobs:
  regenerate:
    runs-on: ubuntu-24.04
    container: ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest
    steps:
      - uses: actions/checkout@v6
        with: { submodules: recursive }
      - uses: actions/setup-dotnet@v5
        with: { global-json-file: global.json }
      - uses: ./.github/actions/platform-build-prereqs
      - uses: ./.github/actions/vcpkg-setup
        with:
          platform-identity: ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest
          triplet: x64-linux-hybrid
          vcpkg-cache-path: .vcpkg-cache
          vcpkg-feature-flags: binarycaching
      - uses: actions/download-artifact@v8
        with: { name: cake-host, path: ./cake-host }
      - run: dotnet ./cake-host/Build.dll --target GenerateBindings --rid linux-x64
      - uses: peter-evans/create-pull-request@v7
        with:
          branch: regenerate-bindings/${{ github.run_id }}
          title: "chore(bindings): regenerate SDL2.Core bindings"
```

`workflow_dispatch`-only initially (manual operator trigger when SDL2 baseline bumps). Auto-PR via `peter-evans/create-pull-request` matches the Silk.NET reference pattern documented in the binding-autogen strategy brief.

**Prerequisites:**

- PSTH-I (action.yml multi-path cache) must land first.
- Cake host published-artifact pattern: this workflow consumes the same `cake-host` artifact `release.yml` already publishes, so a small `build-cake-host` reuse step (or a dedicated build step within the workflow) is needed.
- The Stage 1 plan's existing Task 8 was scoped to add a stamp validator at PreFlight; this workflow is a separate `Task 9` candidate or a post-Stage-1 hardening slice. Decide at implementation time.

**Not in scope of this workflow:**

- Production-location flag-flip to `src/SDL2.<Family>/Generated/` (Stage 1 Task 7).
- Family selection (Stage 2 PSTH-B).
- Promotion to release CI (`release.yml`) — that integration is Stage 2.

### Sequencing

- PSTH-A and PSTH-F are pure cleanup; can land any time before Task 4 starts.
- PSTH-B, C, D, E are coupled (B without C is half-done; D depends on B+C settling). Recommended single landing: a dedicated "family-architecture refactor" slice between this plan and Task 4.
- PSTH-G is Phase 6 work; do not touch until Stage 2 ships.
- PSTH-H is independent. Small focused slice with its own commit, any time.
- PSTH-I is prerequisite for PSTH-J. Can land independently before the workflow.
- PSTH-J lands after PSTH-I + post-Stage-1 Task 8 (stamp validator). Tracked as a Stage 1 Task 9 candidate.

---

## Post-Implementation Review Findings (2026-05-16)

After Stage 1 Task 3.5 implementation + production smoke (4m41s container run, 866 emitted symbols, validator gate clean with 0 false-positives and 6 false-negative warnings), a 3-agent code review + maintainer review surfaced a category of bug the slice's own validator architecture cannot catch. This section records the findings so the follow-up slice can land them as if they were part of Task 3.5 from the start.

### Validator wire-format coverage gap (architectural)

The dynapi cross-check validator (Task 11.5.C) compares only export symbol names against `SDL2.exports`. The Watcom DEF-file format encodes nothing about argument types, parameter order, or return-shape — it is a flat list of `++'_NAME'.'SDL2.dll'.'NAME'` entries the dynamic linker consumes by name. The validator is therefore blind to the wire-format ABI of the emitted P/Invoke surface.

This means the production smoke can ship 866 sembol-correct emits **with byte-width-wrong parameter and return types** and the validator stays green. Bugs P0.1–P0.4 below are exactly this class: name-correct, wire-format-wrong. Consumer P/Invokes for those signatures would raise silent ABI corruption at runtime (wrong stack frame, parameter truncation, return-value mis-marshalling) — none of which the dynapi validator catches.

**The Stage 2 Pack-stage `BindingSymbolExistenceValidator`** (already planned in the strategy brief §"Symbol-existence validation guardrail" + PSTH-J workflow) closes the symbol-presence layer at the binary-table boundary via `dumpbin /exports` / `nm -D` / `nm -gU` across the 3 OS RID matrix. But that validator is **also name-only** — binary symbol tables are byname dispatch dictionaries; they don't carry signature metadata (C ABI, DWARF/PDB excluded). So Stage 2 Pack catches "binding declares an unexported function" but does not catch "binding declares a function with the wrong parameter widths."

**Wire-format signature ABI guardrail is a genuine outstanding gap.** Candidate layers, none chosen yet:

- Static C-side cross-check via an independent parser of SDL2 public headers (cost: a second parser to maintain — defeats CppAst single-source advantage).
- Per-RID consumer smoke matrix invoking selected functions with sentinel arguments and asserting return values match expected. The existing `release.yml consumer-smoke` per-RID matrix is the natural home; needs hand-curated function-spot list. Higher coverage = higher curation cost.
- Runtime `Marshal.PrelinkAll` for symbol-presence — same coverage as dynapi/binary-table, no signature gain.

This gap is recorded in the strategy brief and the maintenance playbook. **Not fixable in the follow-up slice — promote to its own Stage 2 / Phase 4 hardening item.**

### P0 — Wire-format bugs (must fix in the follow-up slice, before commit)

Each item carries silent runtime corruption potential. The translator emits correct function names + binds against existing native exports, but parameter/return wire widths diverge from the C ABI.

- **P0.1** [`build/_build/Targets/GenerateBindings/Model/CppAstToPreviewModel.cs:240`] `MapTypedef` SDL_*-prefix fallback fires before chain-resolve to the underlying primitive. `SDL_AudioFormat` (typedef'd to `Uint16`), `SDL_SpinLock` (`int`), `SDL_GameControllerButton` (enum-backed) emit as `IntPtr` (8 bytes) instead of their underlying primitive width. Wire size wrong.
- **P0.2** [`Model/CppAstToPreviewModel.cs:174`] `MapPrimitive`: `CppPrimitiveKind.Long => "int"` is wrong on Linux. C `long` is 64-bit on LP64 platforms (Linux x86_64, Linux arm64, macOS x86_64, macOS arm64); 32-bit only on Windows LLP64. Stage 1 parses Linux headers — Linux `long` is 64-bit; the emit is 32-bit `int`. Map to `nint` (platform-sized) or branch by `TargetSystem`.
- **P0.3** [`Model/CppAstToPreviewModel.cs:156, 197, 217`] Catch-all `_ => "IntPtr"` swallows unknown `CppType` subtypes silently. Any new SDL2 header pattern → silent `IntPtr` without log/warn/throw. Fail-closed (or fail-warning) on unknown types is the safer default.
- **P0.4** [`Targets/GenerateBindings/Emitting/PreviewEmitter.cs:34-78`] `StringBuilder.AppendLine` uses `Environment.NewLine`. Windows-host generation emits CRLF; Linux-host generation emits LF. Generated `.g.cs` content non-deterministic by host, breaking the `.generated-stamp` reproducibility contract. Use explicit `"\n"` writes (or `AppendLine` against a `StringBuilder` whose `NewLine` is forced to `"\n"`).
- **P0.5** [`build/_build.Tests/Unit/Targets/GenerateBindings/Model/CppAstToPreviewModelTests.cs`] Translator semantic test coverage is empty — all tests use `new List<CppCompilation>()` with no functions. `MapType`/`MapTypedef`/`MapPrimitive`/`IsBindableSdl2Export`/dedup/neutral-subtract are zero-coverage. **This is what allowed P0.1–P0.3 to land undetected.** The fix needs synthetic typedef-heavy fixtures or a thin fake `CppFunction`/`CppType` factory; full libclang-integrated tests are infeasible on the Windows test host.

### P1 — Determinism, concurrency, version-match accuracy (must fix in the follow-up slice)

- **P1.1** [`Targets/GenerateBindings/HeaderSet/HeaderSetFingerprintCalculator.cs:35-39`] Cross-host hash divergence: file content reads honour OS-native line endings + path separators. Windows host vs Linux container produces different hashes for the same logical input set. `.generated-stamp`'s reproducibility contract is broken across hosts. Force LF + forward slashes before hashing.
- **P1.2** Synthetic header content not in fingerprint. Dev edits `build/_build/Targets/GenerateBindings/SyntheticHeaders/windows.h` → hash unchanged → stale stamp says regen is unnecessary. Add the synthetic root to the input-set hash.
- **P1.3** [`build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs:97-103`] `AddGenerateBindings` smoke does not chain `AddData()` though `GenerateBindingsTask` requires `IDynapiManifestRepository` from that cluster. Production works (Program.cs orders correctly) but the DI smoke test doesn't certify completeness. Pattern mismatch vs `AddPreFlightCheck` / `AddHarvest` / `AddPackageConsumerSmoke` smokes.
- **P1.4** [`Targets/GenerateBindings/GenerateBindingsTask.cs:60-78`] `_log.Information(...)` inside PLINQ `.Select(...)` lambda. Cake `ICakeLog` thread-safety is undocumented; concurrent writes from 8 parallel view tasks may interleave. Capture per-view logs into a thread-safe accumulator (`ConcurrentBag<string>` or pre-built log lines) and flush sequentially post-PLINQ.
- **P1.5** PLINQ safety claim made without empirical proof. Maintainer-verified via CppAst.NET source (`CppParser.ParseInternal` creates own `CXIndex.Create()` per call, no static mutable state) — but no stress test in repo. Spec §10.3 explicitly parks the verification; the implementation pre-empted it. Document the verification source in the task code comment; add a basic stress test (8 views × N iterations).
- **P1.6** [`Targets/GenerateBindings/Parsing/LibclangVersionAsserter.cs:21,30`] Version check uses `resolvedVersion.Contains("20.1")` substring. Future libclang `20.10.x` false-matches. Use `Regex.IsMatch(version, @"\b20\.1\.\d+\b")` or version-parse + compare.
- **P1.7** [`Targets/GenerateBindings/GenerateBindingsTask.cs:131-139`] `triplet.Contains("linux", StringComparison.OrdinalIgnoreCase)` weak match — `linux-experimental-anything` would pass. Use `triplet.EndsWith("-linux-hybrid", ...)` against the manifest-declared canonical triplet.
- **P1.8** [`build/_build/Data/BindingGeneration/DynapiManifestRepository.cs:62-64`] `_context.GetFiles(globPath).OrderByDescending(file => file.FullPath, StringComparer.OrdinalIgnoreCase).First()` lex-sorts under `buildtrees/sdl2/src/*`. A version downgrade in vcpkg with a stale newer leftover buildtree on disk picks the stale (wrong) manifest. Restrict to single match (fail-closed on multiple) or use vcpkg version-aware sort.

### P2 — Design (ADR-002 / ADR-003 compliance) + dead surface + stale comments

- **P2.1** [`Targets/GenerateBindings/GenerateBindingsTask.cs:81-119`] `ValidatePublicApiCoherenceAsync` is business logic in the task body. Per ADR-002 §2.2 the task is an orchestrator; extract to `BindingPublicApiCoherenceChecker` collaborator (sealed class, single method, takes `PreviewBindingModel` + `FilePath` manifestPath + `SeverityProfile`, returns `ValidationReport`). Task calls collaborator + handles `CakeException` translation.
- **P2.2** [`Targets/GenerateBindings/Sdl2CoreGenerationConfig.cs`] `Default` static usage couples task to global. Inject via DI as singleton. Bridges to PSTH-D manifest centralization.
- **P2.3** ✅ Landed in P2-α slice (2026-05-16). [`Targets/GenerateBindings/Sdl2CoreGenerationConfig.cs`] `OwnedPrefixes` + `DeferredDeclarations` removed from the record + `Default` factory; matching test assertions dropped. Both concepts re-home in Task 4 at their proper Model/ locations; the retired literal values are kept here as the forward-reference seed (so Task 4 doesn't have to mine `git log`):
  - `OwnedPrefixes` literal `["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"]` → moves to `build/_build/Targets/GenerateBindings/Model/CoreOwnedTypeMap.cs` via `CoreOwnedTypeMap.CreateSdl2Core()` (Task 4 §Step 2 — implementation snippet added 2026-05-16; previously the file was named in §"Files" with no body).
  - `DeferredDeclarations` literal `["SDL_SysWMinfo", "SDL_SysWMmsg"]` → moves to `build/_build/Targets/GenerateBindings/Model/KnownUnsupportedDeclarationPolicy.cs` via `KnownUnsupportedDeclarationPolicy.CreateSdl2Core()` with category `deferred-to-stage-2` and the documented reason string (Task 4 §Step 2 — entries added to the factory snippet 2026-05-16; previously the factory listed only the C-variadic class despite §"Component layout" + §2098 + §2127 promising SysWM coverage here).
  Removing the placeholder fields from `Sdl2CoreGenerationConfig` now avoids creating a translator-filter consumer that Task 4 would have to rip out.
- **P2.4** ⚠ **Retracted (2026-05-16) — original "dead surface" framing was wrong.** `IGeneratedStampRepository` + `GeneratedStampRepository` are **forward-looking infrastructure for Task 8** (`BindingGenerationCoherenceValidator` PreFlight stamp drift validator), as established in [`docs/binding-autogen/binding-autogen-strategy-brief.md`](../../binding-autogen/binding-autogen-strategy-brief.md) §"vcpkg-state coherence guardrail" and [`docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md`](../specs/2026-05-15-binding-generator-local-output-loop-design.md) §"Out of scope". Removing would force re-adding the same surface when Task 8 lands. **Keep as-is** until Task 8 wires the consumer.
- **P2.5** ⚠ **Retracted (2026-05-16) — same reason as P2.4.** `HeaderSetFingerprintCalculator` produces the `header_set_sha256` field of the Task 8 stamp. Strategy-brief §"vcpkg-state coherence guardrail" steps 1–2 are the consumer. **Keep as-is** until Task 8.
- **P2.6** 🟡 **Partially un-actionable (2026-05-16) — the proposed rollback is cascade-locked by the public Cake Frosting Task convention.** `GenerateBindingsTask` is `public sealed class` like all 10 sibling tasks; CS0051 propagates `public` to every type appearing in the task's ctor signature or in any DI-collaborator's public method signature. The eight types listed (`HeaderSetResolver`, `CppAstParseResult`, `CppAstParseRunner` + `ICppAstParseRunner`, `ParseDiagnosticFormatter`, `PlatformParseView`, `PlatformConditionKind`, `ResolvedHeaderSet`, `Sdl2CoreGenerationConfig`) all transitively reach a public Task ctor and therefore cannot flip without first changing the Task-visibility convention repo-wide. `Sdl2CoreGenerationConfig` was already internal (consumed only via the `Default` static); no other in-scope type stays off the public signature graph. Inline code comments at the `HeaderSetResolver` + `ICppAstParseRunner` class declarations record this finding so a future reader doesn't re-attempt the rollback. **Defer until** a Task-visibility-convention slice flips all 10 sibling tasks to `internal sealed class` together.
- **P2.7** [`Targets/GenerateBindings/Model/CppAstToPreviewModel.cs:148-242`] Type-mapping algorithm hidden in private static methods. Per `docs/knowledge-base/extraction-guidelines.md` §"Private methods are a smell when ... they contain business rules", extract to a `TypeMappingPolicy` sealed class. Independent test surface, smaller `CppAstToPreviewModel`, decouples translator from mapping rules.
- **P2.8** [`Model/CppAstToPreviewModel.cs`] Typedef recursion in `MapTypedef`/`MapTypedefPointer` is unbounded. Circular SDL2 typedef (none today, possible) → infinite loop. Add max-depth guard.
- **P2.9** [`Model/CppAstToPreviewModel.cs`] `SafeIdentifier` keyword reserved list incomplete. Missing `where`, `volatile`, `using`, `unsafe`, `fixed`, `lock`, `is`, `as`, `new`, etc. Use the canonical keyword set from Roslyn `SyntaxFacts.GetKeywordKind` or equivalent.
- **P2.10** [`Targets/GenerateBindings/GenerateBindingsTask.cs:143`] Raw `Environment.GetEnvironmentVariable("CONTAINER_DIGEST")` — Cake-native violation. Wrap in `ICakeEnvironment.GetEnvironmentVariable` or pass via `BuildContext` property.
- **P2.11** [`build/_build/Host/Paths/PathService.cs`] `IPathService.GetSdl2DynapiExportsGlob` returns raw `string`. Cake-native convention prefers typed `FilePath`/`FilePathCollection`. Either return collection via direct `GetFiles` or document why string is intentional (glob expansion happens in the repository).
- **P2.12** [`PathService.cs:402-403`] `BindingGeneratorSyntheticHeadersRoot` uses multi-segment string `.Combine("a/b/c")`. Cosmetic Cake idiom — prefer chained `.Combine("a").Combine("b").Combine("c")`.
- **P2.13** ✅ Landed in P2-α slice (2026-05-16) — documented, not parameterized. [`build/_build/Host/Paths/PathService.cs:38-50`] `IPathService.GetSdl2DynapiExportsGlob` now carries an explicit `<para>` note explaining the `sdl2` segment is intentional: the dynapi (dynamic-API dispatch table) is an SDL2-Core-only feature. SDL2 satellites (sdl2-image / sdl2-mixer / sdl2-ttf / sdl2-net / sdl2-gfx) have no dynapi manifest, and SDL3 removed the dynapi system entirely; when SDL3 support lands, a distinct accessor with an SDL3-appropriate symbol oracle is the right shape rather than a parameterized version of this one. Parameterizing now would build the wrong abstraction.

### P2 — Stale comments / lies (PSTH-F territory, specific cites)

- **P2.14** [`Targets/GenerateBindings/Parsing/CppAstParseRunner.cs:81`] Comment "SystemIncludeFolders carries only the vcpkg SDL2 header set" — directly contradicted by the next 4 lines which add 2 paths (synthetic + vcpkg). Rewrite the opening claim.
- **P2.15** [`Targets/GenerateBindings/GenerateBindingsTask.cs:17-21`] Top-of-class comment says "binding-generator-entrypoint.sh runs the two targets back to back (EnsureVcpkgDependencies then GenerateBindings)" — wrong since the Dockerfile refactor baked vcpkg state at image build time and entrypoint only runs `GenerateBindings`.
- **P2.16** [`build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs:100-101`] Comment references deleted `IBindingGenerationRunner`.
- **P2.17** [`build/_build/Targets/GenerateBindings/SyntheticHeaders/windows.h` + `SyntheticHeaders/Inspectable.h`] Comments reference a "DeferredDeclarations filter" that doesn't exist in production code (`DeferredDeclarations` is a dead field per P2.3).
- **P2.18** [`build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/PreviewBindingModelData.cs:18,23` + `PreviewParseViewReportTests.cs:19-20`] Fixture data wrongly attributes `SDL_Init` and `SDL_Quit` to `SDL_main.h`. Real location: `SDL.h` (lines 145, 224). The fixture pins the same wrong attribution as the production header-set assumption — see retraction below.
- **P2.19** [`build/_build/Targets/GenerateBindings/HeaderSet/HeaderSetResolver.cs:18-20`] Comment claims "every symbol [SDL.h] would surface is also declared in one of the headers it includes" — false. **5 SDL.h-only declarations:** SDL_Init (line 145), SDL_InitSubSystem (162), SDL_QuitSubSystem (184), SDL_WasInit (200), SDL_Quit (224). The comment must be retracted and link to the RequiredFunctions fallback list (Task 11 / Çözüm C below).

### PSTH-A scope expansion (Preview / Stage 1 naming purge — additions to the original PSTH-A item above)

Three additional "Preview" surfaces escaped the original PSTH-A scope and must be retired in the same purge slice:

- `CppAstToPreviewModel` class name → `CppAstToBindingModel` (or absorbed into the Task 5 model)
- `PreviewBindingModelData` test fixture class → `BindingModelData` (or per-test fixture)
- `Sdl2Preview_<view>` emitted class prefix → `Sdl2Bindings_<view>` (or whatever Task 5/7 naming settles)
- `Janset.Sdl2.Preview` emitted namespace → `Janset.Sdl2` (post-Task 7 production-location flag-flip)
- `PreviewEmitter` emits `// Stage 1 binding-autogen preview output. Placeholder shape; replaced by real emitter at Stage 1 Task 5.` into every generated `.g.cs` file → strip (internal process commentary should not bleed into consumer-visible artifacts)
- `unsafe partial` modifiers on emitted classes without any unsafe operations or partial extensions → drop until/unless used

### P3 — Polish

- **P3.1** CRLF/LF normalization needed before commit — `.gitattributes` enforces LF for `*.cs` but Windows-saved files carry CRLF. `git add --renormalize .` before commit.
- **P3.2** [`tools.cs:547`] `--cpus <N>` no validation — Spectre.Console.Cli accepts 0/negative/huge. Add `Validate()` to bound `[1, 1024]`.
- **P3.3** [`tools.cs:583-590`] No retry on `docker pull` transient failures. Polly-style 3-attempt backoff for local-dev resilience.
- **P3.4** [`tools.cs:708`] `catch (Exception ex)` in `ReadLinuxBaseImage` swallows root cause. Narrow to `JsonException`/`IOException`.
- **P3.5** [`docker/binding-generator.Dockerfile:33-36`] `dotnet-install.sh` curl-pipe without checksum. Microsoft signs the script but the layer doesn't verify. Pin SHA or use `mcr.microsoft.com/dotnet/sdk:10.0` as base on top of linux-builder.
- **P3.6** [`docker/binding-generator.Dockerfile:95-100`] Smoke check error message unfriendly when `SDL2.exports` is missing. Wrap in explicit bash check with descriptive error.
- **P3.7** [`Targets/GenerateBindings/Sdl2CoreGenerationConfig.cs:28-31`] `ExcludedFunctionNames` declared `IReadOnlySet<string>` but initialized as mutable `HashSet`. Use `FrozenSet<string>` (.NET 8+) for honest immutability + faster `Contains` on the hot translator filter path.
- **P3.8** [`build/_build/Host/Cake/CakeFileSystemExtensions.cs:41-53`] `WriteAllTextAsync` ignores `CancellationToken`. Signature lies; honour it.
- **P3.9** [`Targets/GenerateBindings/HeaderSet/HeaderSetResolver.cs:194-219`] `ExcludedHeaders` defensive cruft — `SDL_opengles2_*` sub-headers are already unreachable via the umbrella exclusion of `SDL_opengles2.h`. Either remove or document why defensive listing is preferred.
- **P3.10** [`Targets/GenerateBindings/Emitting/PreviewEmitter.cs:52`] `unsafe partial` modifiers on emitted class without any unsafe ops or partial extensions. Until/unless used, drop.
- **P3.11** [`build/_build.Tests/Unit/Targets/GenerateBindings/Parsing/CppAstParseRunnerTests.cs:32`] Doesn't assert `SystemIncludeFolders` ordering (synthetic-first, vcpkg-second is correctness-critical per production comment).
- **P3.12** [`build/_build.Tests/Unit/Targets/GenerateBindings/Sdl2CoreGenerationConfigTests.cs`] Pins record shape, not behaviour. After PSTH-D manifest centralization, tests should assert the config is loadable from manifest and validates correctly.
- **P3.13** [`build/_build.Tests/Scenarios/GenerateBindings/GenerateBindingsTaskScenarioTests.cs`] Doesn't cover orchestration past parsing (validator integration, neutral-empty guard, write loop). The 3 fail-closed scenarios are the only coverage; happy-path is manual `tools.cs generate-bindings` smoke only.

### Sound (no slip — verified)

The following are confirmed working and should not be re-flagged in a future review:

- ADR-002 task-orchestrator pattern preserved — no `Runner` / `Pipeline` / `Operation` wrappers reintroduced.
- No `[IsDependentOn]` snuck back onto Cake tasks; sequential dispatch lives at the caller boundary (`docker/binding-generator-entrypoint.sh`, `tools.cs setup` host-side).
- Dynapi resolution is vcpkg-only — no WebFetch fallback, no vendored copy.
- BuildKit cache mount semantics correctly limited to local builds (not CI) — verified against [docs.docker.com/build/cache/invalidation](https://docs.docker.com/build/cache/invalidation/) and [depot.dev BuildKit cache in CI](https://depot.dev/blog/how-to-use-buildkit-cache-mounts-in-ci).
- ADR-004 trio pin (CppAst 0.24.0 + libclang 20.1.2 + libClangSharp 20.1.2) honoured at run time (smoke logged `clang version 20.1.2`).
- `ParseDiagnosticFormatter` thread-safe (immutable `readonly string` field, pure format method).
- `CppParser.ParseFile` thread-safe — each call creates its own `CXIndex.Create()` per call; no static mutable state. Source: [CppAst.NET/src/CppAst/CppParser.cs](https://github.com/xoofx/CppAst.NET/blob/main/src/CppAst/CppParser.cs). PLINQ outer-view parallelism is structurally safe.
- slopwatch clean (0 issues) per project exclude list.

### Header strategy correction note (linked from PSTH-A retraction and §"Header set rules")

The original Stage 1 plan assumed SDL.h-exclusion would not drop public surface because "every symbol it would surface is also declared in one of the headers it includes." This assumption is **false**: 5 SDL2 base functions (`SDL_Init`, `SDL_InitSubSystem`, `SDL_QuitSubSystem`, `SDL_WasInit`, `SDL_Quit`) are declared **only** in `SDL.h` (SDL2 release-2.32.10, lines 145, 162, 184, 200, 224). SDL2 monolithic API style differs from SDL3's modular `SDL_init.h`. Maintainer correction (2026-05-16): introduce a `Sdl2CoreGenerationConfig.RequiredFunctions` fallback list — hand-curated P/Invoke declarations (name + return type + parameter list) for symbols the parse loop cannot reach because their declaring header is excluded from the header set. The translator merges this list into the Neutral view's emission set. Visual-organisation nice-to-have: render the fallback declarations at the top of the emitted file (SDL2-CS convention) but not load-bearing — generated code is rarely human-reviewed. Stage 2 PSTH-D may migrate the list into `manifest.json package_families[]` for declarative cross-family management.

### Sequencing for these review findings

P0 and P1 land in a **follow-up slice** named "Stage 1 Task 3.5 — Post-Implementation Review Fixes." This slice fixes the wire-format bugs and the determinism/concurrency issues before Stage 1 Task 4/5 work begins, treating the corrections as if they were part of Task 3.5 from the start.

P2 design items overlap with PSTH-A through PSTH-G refactor scope; group with the family-architecture slice where natural. P2 stale-comment items go into PSTH-F (code-side doc-reference cleanup).

P3 polish bundles into a separate cleanup slice.

The current dirty surface for Stage 1 Task 3.5 **must not commit in its present form**. Two paths considered:

- **Path A**: Reset the dirty surface back to `c00a2cb` baseline, re-implement on top of the review findings (clean history). High effort.
- **Path B (recommended)**: Apply P0 and P1 fixes directly into the dirty surface as if they were part of Task 3.5 from the start, then commit Task 3.5 + corrections as a single landing. P2 and P3 follow as subsequent slices.

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
