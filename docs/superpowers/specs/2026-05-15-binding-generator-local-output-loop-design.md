# Binding Generator Local Output Loop — Stage 1 Slice Design

> **Status:** Draft (2026-05-15).
> **Scope:** Precursor slice landing **before** Stage 1 Task 4 (binding model + merge policy) in `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`. Establishes the local CppAst-output iteration loop so subsequent model + emitter design (Tasks 4-6) iterates against real generated artifacts instead of designing emitter shape blind.
> **Approval status:** Section-by-section approved 2026-05-15 by Deniz; spec written for handoff to `writing-plans` skill.

## 1. Goal

Add a narrow local generation loop that runs the Cake `GenerateBindings` target inside the pinned `linux-builder` Docker container, produces spike-style placeholder output under `artifacts/generated-bindings-preview/sdl2-core/`, and gives reviewers a real CppAst-output artifact to compare against the existing binding-spike outputs (`tools/binding-spike/cppast-platform/bindings/Generated/*.g.cs`) and peer projects (ppy/SDL3-CS, Alimer.Bindings.SDL).

Why this slice exists between Stage 1 Tasks 3 and 4: Tasks 4-6 design the real binding model, merge policy, type mapping, and per-category emitters. Without real CppAst output to iterate against, those tasks design shape blindly — emitter API choices, model record shapes, and translator boundaries lock in without grounded evidence. The local output loop unblocks evidence-driven decisions for the remainder of Stage 1.

Per ADR-002 §2.4 (target-local until reuse pressure), all new code lands under `build/_build/Targets/GenerateBindings/`. Per ADR-003, persisted contracts (`.generated-stamp` schema, when it materializes) belong under `build/_build/Data/BindingGeneration/`.

## 2. Approved Scope

### 2.1 In-scope deliverables

| Surface | New / Modified | Purpose |
| --- | --- | --- |
| `docker/binding-generator.Dockerfile` | New | Derived from `linux-builder`, adds .NET SDK + NuGet restore + source COPY layers. Source COPY uses `.dockerignore` to exclude `bin/`, `obj/`, `vcpkg_installed/`, `.vs/`, `artifacts/`, `.cake-host/`, `external/vcpkg/buildtrees/`, `external/vcpkg/packages/`, `external/vcpkg/downloads/`. |
| `.dockerignore` extension | Modified | Extend existing patterns to support the binding-generator image build context. |
| `tools.cs` — `GenerateBindingsCommand` | New | Spectre.Console.Cli command; orchestrates `docker build` + `docker volume create` + `docker run` via CliWrap. |
| `build/_build/Targets/GenerateBindings/GenerateBindingsTask.cs` | New | Cake `[TaskName("GenerateBindings")]`, depends on `EnsureVcpkgDependencies`, drives the runner. |
| `build/_build/Targets/GenerateBindings/BindingGenerationRunner.cs` | New | Cake-aware sealed class. Wires `HeaderSetResolver` → `CppAstParseRunner` → `CppAstToPreviewModel` → `PreviewEmitter`; writes outputs through Cake-native IO. |
| `build/_build/Targets/GenerateBindings/Model/` | New, PURE (no Cake) | `PreviewBindingModel`, `PreviewParseView`, `PreviewFunction`, `PreviewParameter` records; `CppAstToPreviewModel` translator. |
| `build/_build/Targets/GenerateBindings/Emitting/` | New, PURE (no Cake) | `PreviewEmitter` + `GeneratedFileSet` record. Spike-style `Commands.g.cs` per parse view + JSON sidecar. |
| `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs` | Modified | Extend `AddGenerateBindings()` registration. |
| `build/_build/Host/Paths/PathService.cs` (+ `IPathService`) | Modified | Add two **temporary** properties: `GenerateBindingsPreviewRoot`, `GetGenerateBindingsPreviewFamilyRoot(string family)`. Retire when Task 5/7 land real emitters + production-location flag flip + family-aware path API. |
| `build/_build/Program.cs` | Modified | Register `AddGenerateBindings()` (already partial). |
| Tests under `build/_build.Tests/` | New + modified | Per Section 7. |

### 2.2 Out of scope (deferred to later Stage 1 tasks)

- Real binding model + merge policy + type mapping (Task 4).
- Real per-category emitters: `CsCommandEmitter`, `CsHandleEmitter`, `CsConstantEmitter`, `CsEnumEmitter`, `CsStructEmitter`, `CsCallbackEmitter` (Task 5).
- Wiring SDL2.Core away from `external/sdl2-cs` (Task 7).
- `BindingGenerationCoherenceValidator` PreFlight stamp drift validator (Task 8).
- `.github/workflows/regenerate-bindings.yml` CI surface (Task 8 / post-Stage 1).
- Family selection (`--family` flag) — Stage 1 hardcodes SDL2.Core.
- Public API snapshot testing (Task 9).
- Committed generated source under `src/SDL2.Core/Generated/` — Stage 1 output is gitignored under `artifacts/generated-bindings-preview/`.

### 2.3 Commit boundary

Recommended: single commit folding Tasks 1-3 dirty work + this slice's additions ("Stage 1 foundations + local generation loop"). Alternative: two commits (Tasks 1-3 corrections; then this slice). Maintainer decides at landing time.

## 3. Architecture & Slice Boundary

### 3.1 Cross-OS bind-mount constraint

Bind-mounting the entire Windows-host repo into a Linux container that runs `dotnet build` / `vcpkg install` corrupts host `bin/`, `obj/`, and NuGet state by writing Linux paths into MSBuild artifacts (`obj/project.assets.json`, `*.nuget.g.props`). The host build then chokes on stale lock/path entries and forces clean rebuild after every container session.

Mitigation: **COPY the repo INTO a derived image** instead of bind-mounting the entire tree. The image becomes a self-contained snapshot; the host filesystem is never written to by the container's `dotnet` / `vcpkg` invocations. Output is the only bind-mount, and it is write-target only — no state contamination.

This constraint matches the related memory `feedback_cross_os_container_mount.md` (durable feedback recorded 2026-05-15).

### 3.2 Slice boundary

```
Host (Windows / macOS / Linux)
  └─ dotnet run --file tools.cs -- generate-bindings
        │
        ├─ docker build  → janset-binding-generator:focal-latest (local-only, no GHCR push)
        ├─ docker volume create janset-vcpkg-cache (idempotent)
        └─ docker run --rm \
              -v janset-vcpkg-cache:/vcpkg-cache \
              -v "${repoRoot}/artifacts/generated-bindings-preview/sdl2-core:/output" \
              -e VCPKG_DEFAULT_BINARY_CACHE=/vcpkg-cache \
              -e VCPKG_BINARY_SOURCES=files,/vcpkg-cache,readwrite \
              janset-binding-generator:focal-latest \
              dotnet run --project build/_build -- --target GenerateBindings --output /output

Container (derived from linux-builder)
  └─ Cake.Frosting dispatches:
        ├─ EnsureVcpkgDependencies   (existing — bootstraps vcpkg + installs x64-linux-hybrid)
        └─ GenerateBindings          (new)
              ├─ assert Runtime.Triplet starts with "linux-"
              ├─ HeaderSetResolver.ResolveSdl2CoreHeaders(...)
              ├─ for each PlatformParseView in PlatformCatalog.CreateSdl2Catalog():
              │     CppAstParseRunner.Parse(...) → CppAstParseResult
              ├─ CppAstToPreviewModel.Translate(parseResults) → PreviewBindingModel
              ├─ PreviewEmitter.Emit(model) → GeneratedFileSet  (PURE; no Cake)
              └─ BindingGenerationRunner writes file set through Cake.Core.IO to /output

Host (post-exit)
  └─ artifacts/generated-bindings-preview/sdl2-core/
        ├─ Platform/Neutral/Commands.g.cs
        ├─ Platform/WindowsDesktop/Commands.g.cs
        ├─ Platform/WinRT/Commands.g.cs
        ├─ Platform/GDK/Commands.g.cs
        ├─ Platform/Linux/Commands.g.cs
        ├─ Platform/MacOS/Commands.g.cs
        ├─ Platform/IOS/Commands.g.cs
        ├─ Platform/Android/Commands.g.cs
        └─ parse-views.json
```

### 3.3 What this slice deliberately is not

- Not a generator architecture redesign — generator architecture is fixed by `docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md`.
- Not a production output path — `src/SDL2.Core/Generated/` flag-flip lands at Task 7.
- Not a determinism contract — `.generated-stamp` PreFlight validator lands at Task 8.

## 4. Component Layout

### 4.1 New / modified files

```text
docker/
├── binding-generator.Dockerfile          NEW — derives from manifest.runtimes[linux-x64].container_image
└── .dockerignore                          MODIFIED — extend for binding-generator build context

tools.cs                                    MODIFIED — add GenerateBindingsCommand

build/_build/
├── Host/Paths/PathService.cs              MODIFIED — add temporary GenerateBindingsPreviewRoot + family helper
├── Targets/GenerateBindings/
│   ├── GenerateBindingsTask.cs            NEW — [TaskName("GenerateBindings")]
│   ├── BindingGenerationRunner.cs         NEW — Cake-side runner
│   ├── ServiceCollectionExtensions.cs     MODIFIED — extend AddGenerateBindings()
│   ├── Model/                              NEW, PURE (no Cake)
│   │   ├── PreviewBindingModel.cs
│   │   ├── PreviewParseView.cs
│   │   ├── PreviewFunction.cs
│   │   ├── PreviewParameter.cs
│   │   └── CppAstToPreviewModel.cs        (CppAst → record translator; port of spike MapType / MapPrimitive logic)
│   └── Emitting/                            NEW, PURE (no Cake)
│       ├── PreviewEmitter.cs               (string emit; returns GeneratedFileSet)
│       ├── GeneratedFile.cs                (record { string RelativePath, string Content })
│       └── GeneratedFileSet.cs             (record { IReadOnlyList<GeneratedFile> Files, string ParseViewReportJson })

build/_build.Tests/Unit/Targets/GenerateBindings/
├── Emitting/
│   ├── PreviewEmitterTests.cs              NEW
│   └── PreviewParseViewReportTests.cs      NEW
└── Model/                                  (CppAst → model translator tests dropped — see Section 7)

build/_build.Tests/Scenarios/GenerateBindings/
└── GenerateBindingsTaskScenarioTests.cs    NEW — fail-closed paths only

build/_build.Tests/Unit/CompositionRoot/
└── ServiceCollectionExtensionsSmokeTests.cs  MODIFIED — assert AddGenerateBindings() resolves
```

### 4.2 PathService temporary additions

```csharp
// IPathService (build/_build/Host/Paths/PathService.cs)
DirectoryPath GenerateBindingsPreviewRoot { get; }
DirectoryPath GetGenerateBindingsPreviewFamilyRoot(string family);

// PathService implementation
public DirectoryPath GenerateBindingsPreviewRoot
    => ArtifactsDir.Combine("generated-bindings-preview");

public DirectoryPath GetGenerateBindingsPreviewFamilyRoot(string family)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(family);
    return GenerateBindingsPreviewRoot.Combine(family);
}
```

**Retirement criteria (recorded for future agents):**

> `GenerateBindingsPreviewRoot` + `GetGenerateBindingsPreviewFamilyRoot(string family)` are Stage 1 scratch-loop scaffolding. They retire when (a) real emitters land in Task 5, (b) Task 7 wires SDL2.Core to `src/SDL2.<Family>/Generated/`, and (c) a per-family path API is designed that integrates with `build/manifest.json package_families[].family_id` rather than ad-hoc string keys. The replacement API should reuse the same family-id resolver pattern used by `ResolveVersionsFromManifest` so generation, harvest, and pack stages share one family identity model. **Do not consume these temporary properties from any code outside `build/_build/Targets/GenerateBindings/` or `tools.cs generate-bindings`.**

### 4.3 Dockerfile shape

```dockerfile
# syntax=docker/dockerfile:1.7
# docker/binding-generator.Dockerfile

ARG BASE_IMAGE
FROM ${BASE_IMAGE}

# Layer A — .NET SDK pinned via global.json
COPY global.json /tmp/
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && chmod +x /tmp/dotnet-install.sh \
 && /tmp/dotnet-install.sh --jsonfile /tmp/global.json --install-dir /usr/share/dotnet
ENV PATH="${PATH}:/usr/share/dotnet"
ENV DOTNET_ROOT=/usr/share/dotnet

# Layer B — NuGet restore (rebuilds only when csproj / CPM / global.json change)
COPY Directory.Packages.props Directory.Build.props global.json /workspace/
COPY NuGet.config* /workspace/
COPY build/_build/Build.csproj /workspace/build/_build/
COPY build/_build.Tests/Build.Tests.csproj /workspace/build/_build.Tests/
RUN cd /workspace && dotnet restore build/_build/Build.csproj --locked-mode

# Layer C — repo source COPY (rebuilds on every code change; deps stay cached)
COPY . /workspace
WORKDIR /workspace
```

`BASE_IMAGE` is passed by `tools.cs` from `build/manifest.json runtimes[linux-x64].container_image` (single source of truth per AGENTS.md §"Configuration File Relationships"). Never hardcoded.

### 4.4 BuildContext does NOT change

No new properties on `BuildContext`, no new fields on `ParsedArguments`, no new `Option<T>` definitions in `Host/Cli/Options/`. Existing surfaces provide everything:

- `context.Runtime.Triplet` → `"x64-linux-hybrid"` inside the container (already from `IRuntimeProfile`)
- `context.Paths.GetVcpkgInstalledTripletDir(triplet)` → SDL2 header source (already from `IPathService`)
- `context.Paths.GetGenerateBindingsPreviewFamilyRoot("sdl2-core")` → output root (new temporary helper above)
- `context.CancellationToken` → forwarded to runner (already from `BuildContext`)

This keeps the slice scope tight: zero CLI ceremony for an output path that has one canonical default. CLI overrides return only when production-location flag-flip needs them (Task 7+).

### 4.5 Pure / Cake-aware split

Per architecture spec §"Pure vs Cake split":

| Component | Purity |
| --- | --- |
| `PlatformCatalog`, `PlatformParseView`, `PlatformCondition` | PURE (already exists) |
| `CppAstParseRunner`, `CppAstParseResult`, `ParseDiagnosticFormatter` | PURE (already exists) |
| `PreviewBindingModel` + records + `CppAstToPreviewModel` translator | PURE (new) |
| `PreviewEmitter`, `GeneratedFile`, `GeneratedFileSet` | PURE (new) |
| `HeaderSetResolver`, `HeaderSetFingerprintCalculator` | Cake-aware (already exists; reads filesystem via `ICakeContext`) |
| `GenerateBindingsTask`, `BindingGenerationRunner` | Cake-aware (new) |
| `GenerateBindingsRequest` | Pure record holding `DirectoryPath` (Cake-typed but no Cake behavior) |
| `tools.cs GenerateBindingsCommand` | Host-side CLI orchestration (CliWrap → `docker` CLI) |

The `PreviewEmitter` returns an in-memory `GeneratedFileSet`; the Cake-aware `BindingGenerationRunner` writes the set through `context.WriteAllTextAsync(...)`. This keeps emitter pure and unit-testable without `ICakeContext`.

## 5. Data Flow / Invocation Lifecycle

### 5.1 Host invocation

```
dotnet run --file tools.cs -- generate-bindings [--rebuild-image] [--no-cache]

GenerateBindingsCommand:
  [1] Validate Docker daemon reachable
        CliWrap "docker version" — exit non-zero → AnsiConsole actionable msg + exit code 66
  [2] Resolve BASE_IMAGE from build/manifest.json
        runtimes[linux-x64].container_image  (currently ":focal-latest")
  [3] docker pull ${BASE_IMAGE}
        — ensures fresh content (mutable pointer may have shifted since last local pull)
  [4] Capture resolved digest (sha256:...)
        via `docker inspect --format='{{index .RepoDigests 0}}' ${BASE_IMAGE}`
        — logged to host stdout for audit trail; passed to container as -e CONTAINER_DIGEST=...
  [5] docker build -t janset-binding-generator:focal-latest \
        --build-arg BASE_IMAGE=${BASE_IMAGE} \
        -f docker/binding-generator.Dockerfile .
        (BuildKit layer cache reuse; --rebuild-image passes --no-cache)
  [6] docker volume create janset-vcpkg-cache  (idempotent)
  [7] Ensure host bind-mount target exists:
        artifacts/generated-bindings-preview/sdl2-core/
  [8] docker run --rm \
        -v janset-vcpkg-cache:/vcpkg-cache \
        -v "${repoRoot}/artifacts/generated-bindings-preview/sdl2-core:/output" \
        -e VCPKG_DEFAULT_BINARY_CACHE=/vcpkg-cache \
        -e VCPKG_BINARY_SOURCES=files,/vcpkg-cache,readwrite \
        -e CONTAINER_DIGEST=${digest} \
        janset-binding-generator:focal-latest \
        dotnet run --project build/_build -- --target GenerateBindings --output /output
  [9] Stream container stdout/stderr to host AnsiConsole
  [10] Propagate container exit code
```

### 5.2 Container invocation (Cake target chain)

```
EnsureVcpkgDependencies (existing target):
  - bootstrap-vcpkg.sh (idempotent — checks for ./external/vcpkg/vcpkg binary)
  - vcpkg install --triplet x64-linux-hybrid
      --overlay-triplets vcpkg-overlay-triplets/
      --overlay-ports vcpkg-overlay-ports/
  - reads/writes /vcpkg-cache (Docker volume) via VCPKG_DEFAULT_BINARY_CACHE
  - result: vcpkg_installed/x64-linux-hybrid/include/SDL2/*.h

GenerateBindings (new target; depends on EnsureVcpkgDependencies):
  - assert context.Runtime.Triplet starts with "linux-" → fail-closed otherwise
  - libclang version assertion (Section 8.2)
  - resolve canonical headers:
        headerSet = HeaderSetResolver.ResolveSdl2CoreHeaders(
            context.Paths.GetVcpkgInstalledTripletDir(triplet),
            triplet)
  - parse loop:
        var parseResults = new List<CppAstParseResult>();
        foreach (var view in PlatformCatalog.CreateSdl2Catalog().ParseViews):
            parseResults.Add(CppAstParseRunner.Parse(headerSet, view));
  - translate:
        var model = CppAstToPreviewModel.Translate(parseResults);
  - assert neutral view non-empty:
        if (model.Views.First(v => v.Name == "Neutral").Functions.Count == 0)
            throw new CakeException("Neutral parse view returned 0 SDL2 functions...");
  - emit:
        var fileSet = PreviewEmitter.Emit(model);
  - write through Cake-native IO:
        var outputRoot = context.Paths.GetGenerateBindingsPreviewFamilyRoot("sdl2-core");
        context.EnsureDirectoryExists(outputRoot);
        await BindingGenerationRunner.WriteAsync(fileSet, outputRoot, context.CancellationToken);
```

### 5.3 Iteration cost profile

| Change | Profile |
| --- | --- |
| Source-only edit (catalog / emitter / model) | Layer C COPY rebuilds (~10s) + container startup + Cake dispatch (~5s) + vcpkg install binary-cache hit (~30s) + parse 8 views (~25s) + emit (~1s) → **~1-2 min** |
| `Build.csproj` / CPM / `global.json` change | + Layer B restore rebuild (~20s) |
| `Dockerfile` change | + Layer A SDK install rebuild (~3-4 min) |
| Fresh clone (cold caches) | First vcpkg install ~10-15 min from source; subsequent runs ~30s via binary cache |
| `vcpkg.json` change | vcpkg install re-evaluates; binary cache hits for unchanged deps |

First run slow, subsequent runs comfortable. Trade-off accepted per 2026-05-15 design discussion.

## 6. Error Handling & Diagnostics

### 6.1 Host-side (`tools.cs`)

| Failure | Behavior |
| --- | --- |
| Docker daemon unreachable | `docker version` exits non-zero → AnsiConsole actionable msg ("Docker daemon not available. Start Docker Desktop / dockerd, then retry."); exit code 66 |
| `docker build` fails | Stream full BuildKit output; propagate exit code |
| Volume create permission denied | Surface raw stderr; exit non-zero (rare on dev hosts) |
| `docker run` non-zero exit | Stream container stdout/stderr live; propagate container exit code (CI parity) |
| Host bind-mount target missing | tools.cs creates host dir before `docker run` |
| Manifest read failure | Fail-closed with manifest path + parse error context |

### 6.2 Cake-side (`GenerateBindingsTask` + `BindingGenerationRunner`)

| Failure | Behavior |
| --- | --- |
| `Runtime.Triplet` doesn't start with `linux-` | `throw new CakeException($"GenerateBindings is Linux-canonical; expected linux-x64 host RID inside linux-builder container; got RID '{Runtime.Rid}' / triplet '{triplet}'. Invoke via 'tools.cs generate-bindings'.")` |
| libclang resolved version doesn't match CppAst expected (Section 8.2) | `throw new CakeException(...)` with version mismatch + remediation pointer |
| `HeaderSetResolver` finds 0 SDL2 headers | Existing behavior: `CakeException("No SDL2 headers matching '*.h' were found at '{path}'.")` |
| `CppAstParseRunner.Parse` returns `compilation.HasErrors` | Existing behavior: `InvalidOperationException` formatted by `ParseDiagnosticFormatter`; task wraps in `CakeException` with parse-view context |
| Neutral view emits 0 functions | `throw new CakeException("Neutral parse view returned 0 SDL2 functions. Header set or macro hygiene likely misconfigured.")` |
| Output dir parent missing | `context.EnsureDirectoryExists(outputRoot)` before write — idempotent |
| Existing output files | Overwritten; loop expects deterministic re-emit |

### 6.3 Logging discipline

- `ICakeLog.Information` for per-parse-view function counts ("Neutral: 412 functions; WindowsDesktop: +28 platform-only; Linux: +19; …").
- `ICakeLog.Information` for resolved libclang version + container digest (audit trail).
- `ICakeLog.Verbose` for header file list and parse-view defines/undefines.
- `ICakeLog.Warning` reserved for non-fatal anomalies (e.g., a documented-exclusion view producing unexpected content).
- Container output streams to host AnsiConsole via `tools.cs` CliWrap pipe.

## 7. Testing

### 7.1 Test scope (this slice = infrastructure, not translator semantics)

Per the 2026-05-15 design discussion ("alt yapı test edelim, Task 4 sonrasıyla alakası yok"), this slice tests **infrastructure wiring**: Cake target dispatch, DI registration, fail-closed paths, emitter output shape. Translator semantic correctness is validated by manual `tools.cs generate-bindings` smoke + visual diff against the binding-spike; rigorous translator unit tests land in Task 4 with the real binding model.

Synthetic fixture headers (`sdl2-mini.h` style) are **not** used — over-cautious for a placeholder pipeline that gets replaced in Task 4-5.

### 7.2 Pure unit tests (Cake-free, fast)

```text
build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/
├── PreviewEmitterTests.cs              — hand-constructed PreviewBindingModel;
│                                          assert per-view file content shape,
│                                          neutral-exclusion logic, deterministic ordering,
│                                          empty-view handling
└── PreviewParseViewReportTests.cs      — JSON sidecar serialization round-trip + shape
```

Test data: `PreviewBindingModelData` centralized inline static class (per testing-guidelines §"Centralized inline data"). No embedded fixtures, no real SDL2 headers in test tree.

### 7.3 Scenario tests (FakeCakeWorld + TargetTestHost — fail-closed paths only)

```text
build/_build.Tests/Scenarios/GenerateBindings/
└── GenerateBindingsTaskScenarioTests.cs
   ├── RunAsync_Should_Throw_CakeException_When_Triplet_Not_Linux
   ├── RunAsync_Should_Throw_CakeException_When_Header_Directory_Missing
   └── RunAsync_Should_Throw_CakeException_When_Neutral_View_Yields_Zero_Functions
```

Tests inject a stub `IBindingGenerationRunner` test-double — assert Cake target orchestration + fail-closed branches without running the real parser. Per testing-guidelines §"Scenario test structure".

### 7.4 Composition root smoke (extend existing pattern)

```text
build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs   (modified)
        — assert AddGenerateBindings() resolves GenerateBindingsTask + collaborators
```

### 7.5 Slice landing verification

```pwsh
# Managed checks
dotnet build build/_build/Build.csproj -c Release
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"

# Manual generation smoke — the canonical Stage 1 acceptance test for this slice
dotnet run --file tools.cs -- generate-bindings

# Visual inspection of output
#   artifacts/generated-bindings-preview/sdl2-core/Platform/<View>/Commands.g.cs  (8 files)
#   artifacts/generated-bindings-preview/sdl2-core/parse-views.json
# Eyeball-diff against:
#   tools/binding-spike/cppast-platform/bindings/Generated/SDL2.Platform.*.g.cs
#   ppy/SDL3-CS function-only output (for shape comparison)
```

## 8. Pinning Strategy

### 8.1 Current pin surface

| Pin | Where | Status |
| --- | --- | --- |
| CppAst | `Directory.Packages.props` CPM | ✓ 0.24.0 |
| `libclang.runtime.linux-x64` | `Directory.Packages.props` CPM | ✓ 20.1.2 |
| `libClangSharp.runtime.linux-x64` | `Directory.Packages.props` CPM | ✓ 20.1.2 |
| .NET SDK 10.x | `global.json` | ✓ pinned |
| linux-builder image tag | `build/manifest.json runtimes[linux-x64].container_image` | ✓ `:focal-latest` (mutable pointer, monthly rebuild cron) |
| Derived `binding-generator.Dockerfile` FROM | `ARG BASE_IMAGE` sourced from manifest | ✓ inherits same pointer |
| GCC 11 in linux-builder | `ubuntu-toolchain-r/test` PPA via `linux-builder.Dockerfile` | ✓ pinned |
| Focal apt packages | `linux-builder.Dockerfile` `apt-get install` (unversioned, latest-within-focal) | ⚠ peer-baseline; not pinned |

### 8.2 New: defensive libclang version assertion

Stage 1 adds runtime version verification at the top of `BindingGenerationRunner.RunAsync`:

```csharp
// Pseudocode — concrete API call resolved during implementation
var resolvedLibclangVersion = clang.clang_getClangVersion();
_log.Information("libclang resolved version: {0}", resolvedLibclangVersion);
const string ExpectedMajorMinor = "20.1";
if (!resolvedLibclangVersion.Contains(ExpectedMajorMinor, StringComparison.Ordinal))
{
    throw new CakeException(
        $"libclang version mismatch: expected {ExpectedMajorMinor}.x (CppAst 0.24.0 trio), got '{resolvedLibclangVersion}'. " +
        $"Check Directory.Packages.props + packages.lock.json. See docs/playbook/binding-generator-maintenance.md §Trio Pinning.");
}
```

Rationale: defensive against future package-restore drift or accidental floating-version regression. Spike findings §7.2 friction #5 documents the failure mode (stack overflow on libclang 21.x against CppAst 0.24.0).

### 8.3 New: maintenance playbook trio version table

`docs/playbook/binding-generator-maintenance.md` gains a "Trio Pinning" table:

| CppAst | libclang.runtime.* | libClangSharp.runtime.* | Status |
| --- | --- | --- | --- |
| 0.24.0 (2025-11-20) | 20.1.2 | 20.1.2 | Stage 1 pinned |
| 0.25+ | TBD | TBD | candidates — see [[Deferred Follow-ups]] §10.1 |

Bump-validation discipline (already in ADR-004) gets restated in the playbook: "Trio versions move as a coordinated set. A CppAst bump drives the libclang/libClangSharp runtime bump and requires re-validating output byte-identity or explainable diff."

### 8.4 New: stamp schema records container digest

Stage 1 `GeneratedStamp` schema (already in dirty surface from Tasks 1-3) gains a `container` block:

```jsonc
{
  "generator": { "gitSha": "...", "version": "stage-1-preview" },
  "toolchain": {
    "cppAst": "0.24.0",
    "libclangRuntime": "20.1.2",
    "libClangSharpRuntime": "20.1.2",
    "dotnetSdk": "10.0.X"
  },
  "container": {
    "image": "ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest",
    "digest": "sha256:..."   // resolved via tools.cs `docker inspect`, passed as env var
  },
  "vcpkg": { "manifestHash": "...", "baseline": "...", "sdl2Version": "2.32.10" },
  "headerSet": { "fingerprint": "...", "headerCount": 65 },
  "platformCatalog": { "viewsHash": "..." }
}
```

`container.digest` captures the resolved sha256 of the linux-builder image at generation time. Mutable pointer (`:focal-latest`) shifts on monthly rebuild; digest in the stamp gives objective audit ("Did the base image change between regen A and regen B? Did the output change too?").

`.generated-stamp` is gitignored in this slice (under `artifacts/generated-bindings-preview/`); committed stamp under `src/SDL2.<Family>/Generated/` lands at Task 7 alongside the production-location flag-flip.

### 8.5 Deliberate non-pinning

| Non-pin | Why |
| --- | --- |
| Apt package version pins (`apt-get install clang-XX=<exact-version>`) | Peer-baseline doesn't pin; focal patch-level updates ABI-stable; small value, large maintenance burden. |
| linux-builder image digest pin | Peer-baseline mutable major tag (SDL3-CS, SkiaSharp, Silk.NET); manifest's mutable pointer is single-source-of-truth across local + CI. |
| vcpkg port digest pin | vcpkg baseline already version-locks ports. |
| Self-hosted runner | Silk.NET-style Windows-specific solution; Linux container approach side-steps the MSVC-header-drift class of problem. |

### 8.6 Peer comparison (research input 2026-05-15)

| Dimension | ppy/SDL3-CS | SkiaSharp | Silk.NET | Janset (us) |
| --- | --- | --- | --- | --- |
| Base OS | `ubuntu:24.04` mutable | `debian:11`/`13` per-major | self-hosted Win Server 2022 | `ubuntu:20.04 focal-latest` |
| Image digest pin | no | no | n/a | no |
| Lockfile | none | `VERSIONS.txt` text manifest | none | CPM `Directory.Packages.props` + `packages.lock.json` |
| libclang | NuGet `libclang` 17.0.4 | n/a | ClangSharp 15.0.2 (legacy) | CppAst 0.24.0 + libclang.runtime 20.1.2 trio |
| Sysroot/SDK pin | apt-glob | per-arch crosscompile | **frozen VS17.4.3 + Win11 SDK 22621** | inherits focal apt + GCC 11 PPA |
| Determinism docs | none | per-pin inline | `runner-setup.md` explicit | this spec + maintenance playbook |

Our pinning is stricter than ppy/SDL3-CS, broadly comparable to SkiaSharp, less rigid than Silk.NET — but Silk.NET pays the cost in self-hosted runners that aren't justified for Linux-canonical generation.

## 9. Documentation Updates

The slice ships with the following doc updates (same commit or immediate follow-up):

| Doc | Update |
| --- | --- |
| `docs/binding-autogen/binding-autogen-strategy-brief.md` §Plan Shape | "Stage 1 starts with scratch output under `artifacts/generated-bindings-preview/sdl2-core/`; flip to `src/SDL2.<Family>/Generated/` lands at Task 7. Local invocation `tools.cs generate-bindings`, Linux-container canonical." |
| `docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md` §Documentation and Sequencing | Add precursor slice between Tasks 3 and 4. |
| `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md` | Insert new "Task 3.5 (or renumber 4): Local Output Loop" referencing this spec. Document `PreviewEmitter` + `PreviewBindingModel` retirement criteria (replaced by Task 4-5 real model + emitters). Document `IPathService.GenerateBindingsPreviewRoot` retirement criteria. |
| `docs/playbook/binding-generator-maintenance.md` | Add: (a) Trio Pinning table (Section 8.3); (b) `tools.cs generate-bindings` local loop section; (c) vcpkg cache volume hygiene (`docker volume rm janset-vcpkg-cache` to nuke); (d) `--rebuild-image` semantics; (e) "Post-Stage 1 trio revalidation" maintenance item (Section 10.1). |
| `docs/binding-autogen/README.md` | Surface the slice in Stage 1 status block. |
| `docs/playbook/local-development.md` | Cross-reference: "Generated binding loop: `tools.cs generate-bindings` — see binding-generator-maintenance.md". |
| `docs/parking-lot.md` | Two new entries: "Post-Stage 1 CppAst trio revalidation against latest" + "Deep-dive: system header / glibc / sysroot impact on SDL2 AST byte-identity" (Section 10). |
| `.gitignore` | Add `artifacts/generated-bindings-preview/` if not already covered by `artifacts/`. |

## 10. Deferred Follow-ups

Items intentionally parked at this design's landing time. Cross-linked from `docs/parking-lot.md` and `docs/playbook/binding-generator-maintenance.md`.

### 10.1 Post-Stage 1 CppAst trio revalidation against latest

- **Trigger:** Stage 1 (Tasks 4-10) lands and ships a stable SDL2.Core regeneration baseline.
- **Action:** Attempt to bump the trio to absolute-latest available versions on NuGet. As of 2026-05-15:
  - `libclang.runtime.linux-x64` latest: check NuGet — known 21.1.8.x line existed
  - `libClangSharp.runtime.linux-x64` latest: check NuGet — known 21.1.8.2
  - CppAst latest: check NuGet — may have released 0.25+ since 2025-11-20
- **Why parked:** CppAst 0.24.0 was deliberately matched with libclang 20.1.2 because libclang 21.x crashed CppAst's AST visitor (spike findings §7.2 friction #5). A newer CppAst release may align with libclang 21.x. Bump must be coordinated trio (ADR-004 rule) and re-validated against:
  - Byte-identical regeneration (or explainable diff)
  - Stage 1 SDL2.Core public API snapshot (Task 9)
  - All 8 platform parse views still parse without `compilation.HasErrors`
- **Why not now:** The current pin works, the Stage 1 generator output is the priority, and a mid-Stage trio bump introduces a parallel investigation that would derail completion. Maintainer comfort note (Deniz, 2026-05-15): "20.1.x içime sinmedi ama şimdilik concern'ümüz olmamalı".

### 10.2 Deep-dive: system header / glibc / sysroot impact on SDL2 AST byte-identity

- **Trigger:** A real byte-identity regression appears across regen runs, OR Stage 2 satellite generation surfaces a sysroot-related parse difference between local and CI, OR the linux-builder monthly rebuild starts producing materially different output across rebuild cycles.
- **Action:** Investigate the depth of the system-header-drift risk. Topics:
  - Does focal's `apt-get upgrade -y` (monthly rebuild cron) actually shift `<stdint.h>`, `<stddef.h>`, `<X11/Xlib.h>`, `<wayland-client.h>` contents enough to alter the SDL2 AST?
  - Is the libclang.runtime NuGet's bundled libclang.so reading from focal's `/usr/include/` or from its own bundled headers? If the latter, glibc patch updates don't matter; if the former, they do.
  - Empirical: regenerate output at digest D1 (today's focal-latest) and digest D2 (next month's rebuild); diff. If diff is zero, current pinning is sufficient. If diff is non-zero, surface the diff source and decide:
    - Pin focal apt packages explicitly?
    - Pin linux-builder image to immutable `:focal-<yyyymmdd>-<sha>` tag in manifest?
    - Accept controlled drift with `.generated-stamp` audit trail?
- **Why parked:** SDL2's public API uses portable C primitives whose typedef stability across glibc 2.28-2.39 is empirically high. The cost of investigating drift now without evidence of a real regression is higher than the cost of investigating later when a regression provides ground truth. Spike findings §6 documents the theoretical risk: "system headers are part of the distro. Pinning an Ubuntu image digest = pinning glibc + gcc-includes + .NET SDK + everything libclang reads. Single source of truth." This deep-dive operationalizes that observation when (and if) it materializes.

## 11. References

### Operating rules and decisions
- `AGENTS.md` — operating rules; "Pure code stays pure"; "Cake nativeness is a hard rule at build boundaries"; "Configuration File Relationships" (manifest single source of truth).
- `docs/decisions/2026-05-05-target-centric-build-host.md` — ADR-002 target-centric build host pattern.
- `docs/decisions/2026-05-12-build-host-data-layer.md` — ADR-003 contract-centric data layer.
- `docs/decisions/2026-05-14-binding-autogen-toolchain.md` — ADR-004 CppAst toolchain trio coordination.

### Knowledge base
- `docs/knowledge-base/extraction-guidelines.md` — collaborator extraction.
- `docs/knowledge-base/testing-guidelines.md` — `FakeCakeWorld`, `TargetTestHost`, fixture data policy, centralized inline data.

### Stage 1 design + plan
- `docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md` — overall generator architecture.
- `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md` — Stage 1 implementation plan (this slice precedes Task 4).
- `docs/binding-autogen/binding-autogen-strategy-brief.md` — accepted strategy.

### Research
- `docs/binding-autogen/research/binding-autogen-spike-findings.md` §5.3, §6, §7.2 — ClangSharp ↔ libclang coupling, Linux pinning rationale, trio version mismatch incident.
- `docs/binding-autogen/research/binding-autogen-approaches.md` — toolchain survey including peer projects.

### Container infrastructure
- `docker/linux-builder.Dockerfile` — base image this slice derives from.
- `.github/workflows/build-linux-container.yml` — linux-builder GHCR publication workflow.
- `.github/actions/vcpkg-setup/action.yml` — CI vcpkg cache pattern this slice mirrors locally via Docker volume.

### Memory
- `feedback_cross_os_container_mount.md` (auto-memory) — durable feedback: COPY-into-image, not bind-mount, for cross-OS Linux container dev loops.
