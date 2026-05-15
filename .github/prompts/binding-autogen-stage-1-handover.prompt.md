---
name: "Binding autogen Stage 1 handover"
description: "Handover prompt for the next agent entering janset2d/sdl2-cs-bindings on branch spike/binding-autogen-sdl2-gfx after Stage 1 Tasks 1-3 were partially implemented in a dirty working tree, then corrected for Cake-native / ADR-003 boundaries. No commit has been made. Recommended next: review the dirty diff, then design a local Linux-container output loop before Task 4."
argument-hint: "Optional override: ask for a focused review, continue implementation, or inspect a specific Stage 1 task. Defaults to reviewing current work and planning the local output loop before Task 4."
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

# Binding Autogen Stage 1 Handover

You are entering `janset2d/sdl2-cs-bindings` on branch `spike/binding-autogen-sdl2-gfx` after a long Stage 1 binding-autogen implementation session. The working tree is intentionally dirty. **No commit has been made.** Deniz explicitly said he will decide when to commit.

Your first job is not to sprint into Task 4. Your first job is to understand the current dirty state, preserve the architectural corrections, and discuss/implement the local generation-output loop before deeper binding model/emitter work.

## First Principle

> Treat this handover as current-as-of-authoring (2026-05-15, ~16:50 Istanbul time), but verify against the live repo before acting. Run `git status --short`, inspect the dirty diff, and read the canonical docs listed below. Do not assume this prompt is more authoritative than code + current docs.

## Non-Negotiable Rules

- **Approval gate:** no commit, no deployment, no publish, no CI apply without explicit Deniz approval.
- **Cake-native build host:** in `build/_build`, do not use raw `System.IO` file/path APIs at build boundaries. Use Cake `DirectoryPath` / `FilePath`, `ICakeContext`, `CakeFileSystemExtensions`, and `CakeJsonExtensions`.
- **ADR-003 Data rule:** if the build host owns a persisted/tool-read file contract, put the model + reader/writer repository under `build/_build/Data/<Contract>/`.
- **Target services are not Data:** generation input discovery (for example reading vcpkg-installed SDL headers for one run) is target behavior, not automatically a repository.
- **No static-class dumping:** use small named collaborators where behavior deserves a name. Avoid static helper piles unless they are genuinely stateless language-level helpers.
- **No invented result shapes:** use `Build.Results.Result<T,TError>` for expected operation failures and `ValidationReport` for multi-check validation.
- **No standalone generator project:** no `src/Janset.SDL2.Bindings.Generator/`, no new test project. The generator lives in the Cake build host.
- **No Windows/macOS production generation path:** production and final local generation are Linux-canonical.

## Mandatory Grounding

Read these before making further changes:

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `docs/plan.md`
4. `docs/decisions/2026-05-05-target-centric-build-host.md` (ADR-002)
5. `docs/decisions/2026-05-12-build-host-data-layer.md` (ADR-003)
6. `docs/knowledge-base/extraction-guidelines.md`
7. `docs/knowledge-base/testing-guidelines.md`
8. `docs/binding-autogen/binding-autogen-strategy-brief.md`
9. `docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md`
10. `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`
11. `docs/playbook/binding-generator-maintenance.md`

The older research docs under `docs/binding-autogen/research/` are useful evidence, but they include frozen pre-2026-05-15 claims. Prefer the strategy brief and this session's corrected docs when they differ.

## Current Git / Working Tree Expectations

Expected branch:

```text
spike/binding-autogen-sdl2-gfx
```

Expected HEAD before this session's dirty work:

```text
1c0b6e6 docs(binding-autogen): sync downstream docs + research retraction banners for 2026-05-15 revisions
c921e44 docs(binding-autogen): fold generator into Cake host, lock Linux-canonical, retract stub speculation
```

Expected dirty tracked/untracked surface at handover:

- `Directory.Packages.props`
- `build/_build/Build.csproj`
- `build/_build/packages.lock.json`
- `build/_build.Tests/packages.lock.json`
- `build/_build/Program.cs`
- `build/_build/Data/ServiceCollectionExtensions.cs`
- `build/_build/Host/BuildContext.cs`
- `build/_build.Tests/Fixtures/FakeCakeWorld.cs`
- `build/_build/Data/BindingGeneration/`
- `build/_build/Targets/GenerateBindings/`
- `build/_build.Tests/Unit/Data/BindingGeneration/`
- `build/_build.Tests/Unit/Targets/GenerateBindings/`
- `docs/binding-autogen/README.md`
- `docs/binding-autogen/binding-autogen-strategy-brief.md`
- `docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md`
- `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`
- `docs/README.md`
- `docs/playbook/binding-generator-maintenance.md`
- `.github/prompts/binding-autogen-stage-1-execution.prompt.md` (pre-existing untracked prompt from this workstream)
- `.github/prompts/binding-autogen-stage-1-handover.prompt.md` (this file, if saved before handoff)

Verify with:

```pwsh
git status --short
git diff --stat
```

## What Was Implemented

### Stage 1 Task 1 — Scaffold GenerateBindings

Implemented:

- Added `CppAst`, `libclang.runtime.linux-x64`, and `libClangSharp.runtime.linux-x64` references to `build/_build/Build.csproj` via `dotnet add`.
- Updated lock files.
- Updated `Directory.Packages.props` comment block to clarify:
  - production `Build.csproj` references only linux-x64 runtime packages;
  - existing win-x64 runtime pins remain for tracked spike projects under `tools/binding-spike`.
- Created `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs`.
- Created `build/_build/Targets/GenerateBindings/Sdl2CoreGenerationConfig.cs`.
- Created `build/_build/Targets/GenerateBindings/GenerateBindingsRequest.cs`.
- Wired `AddGenerateBindings()` into `build/_build/Program.cs`.
- Added `Sdl2CoreGenerationConfigTests`.

Important correction:

- The initial plan snippets used `DirectoryInfo` for `GenerateBindingsRequest`. This was corrected to Cake `DirectoryPath`.

### Stage 1 Task 2 — Header Input + Generated Stamp

Implemented after several architectural corrections:

Target-local header input services:

```text
build/_build/Targets/GenerateBindings/HeaderSet/
├── ResolvedHeaderSet.cs
├── HeaderSetFingerprint.cs
├── HeaderSetResolver.cs
└── HeaderSetFingerprintCalculator.cs
```

Data-layer stamp contract:

```text
build/_build/Data/BindingGeneration/
├── GeneratedStamp.cs
└── GeneratedStampRepository.cs
```

Tests:

```text
build/_build.Tests/Unit/Targets/GenerateBindings/HeaderSet/
├── HeaderSetResolverTests.cs
└── HeaderSetFingerprintCalculatorTests.cs

build/_build.Tests/Unit/Data/BindingGeneration/
└── GeneratedStampRepositoryTests.cs
```

Key discoveries/corrections:

- Header discovery/fingerprinting is **not** a Data repository. It reads vcpkg-installed generation input for one target run, similar to Harvest target services.
- `.generated-stamp` **is** a Data contract. It now has a repository with `SaveAsync` and `LoadAsync`.
- `.generated-stamp` is extensionless JSON, so `GeneratedStampRepository.LoadAsync()` cannot use `CakeJsonExtensions.ToJsonAsync`, which requires `.json`. It uses Cake-native text read + central `DeserializeJson`.
- Fingerprint line-ending normalization now uses `Environment.NewLine`, leaning on Linux-canonical generation instead of manually forcing LF.

### Stage 1 Task 3 — Platform Catalog + CppAst Parse Options

Implemented:

```text
build/_build/Targets/GenerateBindings/Parsing/
├── PlatformCondition.cs
├── PlatformParseView.cs
├── PlatformCatalog.cs
├── CppAstParseResult.cs
├── ParseDiagnosticFormatter.cs
└── CppAstParseRunner.cs
```

Tests:

```text
build/_build.Tests/Unit/Targets/GenerateBindings/Parsing/
├── PlatformCatalogTests.cs
└── CppAstParseRunnerTests.cs
```

Current `PlatformCatalog` shape:

- Neutral
- WindowsDesktop
- WinRT
- GDK
- Linux
- MacOS
- IOS
- Android

Key discovery:

- `CppParserOptions.TargetSystem` defaults to the current host (`windows` on Windows). To enforce the "no `--target` cross-compile flag" strategy, `CppAstParseRunner` explicitly sets:

```csharp
TargetSystem = string.Empty
```

This is intentional. Do not remove it casually.

## What Was Corrected In Docs

Several accepted docs had drift after the implementation/audit:

- `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`
- `docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md`
- `docs/binding-autogen/binding-autogen-strategy-brief.md`
- `docs/binding-autogen/README.md`
- `docs/README.md`
- `docs/playbook/binding-generator-maintenance.md`

Corrections included:

- Removed/updated stale `System.IO` examples in Stage 1 plan.
- Removed stale `dotnet test --filter` commands; Microsoft.Testing.Platform/TUnit in this repo does not support that flag.
- Removed invented `TestWorkspace` and `FakeCakeContext.Quiet()` guidance.
- Corrected the platform-pass spike path from `tools/binding-spike/cppast/generator/Program.cs` to `tools/binding-spike/cppast-platform/generator/Program.cs`.
- Clarified pure-vs-Cake boundary:
  - parser/model/emitter policy can be Cake-free;
  - header/stamp/generated-file IO remains Cake-native;
  - `.generated-stamp` is Data-layer;
  - header input services are target-local.
- Updated future plan snippets to use existing `Build.Results.Result<T,TError>` rather than inventing `BindingGenerationResult`.
- Added `docs/playbook/binding-generator-maintenance.md`, an in-progress maintenance playbook covering:
  - platform macro catalog maintenance;
  - SDL2 vs SDL3 macro differences;
  - generated stamp maintenance;
  - hybrid-static overlay coupling;
  - upstream/vcpkg bump checklist.

## Build Host / Test Host Discoveries

### Build Host

The canonical pattern from ADR-002 / code:

```text
Program.cs
  -> AddHostBuildingBlocks()
  -> AddData()
  -> AddValidators()
  -> Add<Target>()
```

Targets live under:

```text
build/_build/Targets/<CakeTargetName>/
```

Tasks are discovered by `[TaskName]`; do not register task classes in DI.

Data contracts live under:

```text
build/_build/Data/<Contract>/
```

Cross-cutting validators live under:

```text
build/_build/Validation/
```

Expected operation failures use:

```csharp
Build.Results.Result<T, TError>
```

Multi-check validation uses:

```csharp
ValidationReport
```

Task boundaries translate failures into `CakeException`.

### Tests

Use:

- `FakeCakeWorld`
- `TargetTestHost<TTask>`
- `ServiceCollectionTestHost.AddFakeCakeWorld`
- `FixtureLoader`
- Cake fake filesystem

Do not use temp directories or raw `System.IO` in unit/scenario tests unless the test is explicitly integration-boundary coverage.

## Verification Already Run

The following passed after the latest corrections:

```pwsh
dotnet build build/_build/Build.csproj -c Release
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"
```

Latest known test count:

```text
640 succeeded, 2 skipped
```

Also run/verify:

- `ReadLints` on touched files: clean.
- `rg` guard checks for stale `HeaderSetRepository`, `GeneratedStampWriter`, target-local Stamps namespace, raw LF append patterns, and custom `BindingGenerationResult`: no matches except intentional prose in docs where noted.

## Important Open Discussion Before Task 4

Do **not** blindly continue into Stage 1 Task 4 yet.

The next useful discussion is the **local dev generated-output loop**. Deniz raised the correct concern: before implementing binding model/type mapping/emitters, we need to see real generated output and compare it to peer outputs.

Two options were discussed:

### Option 1 — Local Linux Container Loop (Recommended)

Add `tools.cs generate-bindings` earlier than the original Task 8 intent, or add a narrow precursor that runs the Cake host inside the pinned Linux builder container and writes output back to the host workspace.

Why this is recommended:

- Production generation is Linux-canonical.
- Local output and CI output come from the same OS/sysroot/libclang runtime.
- Avoids a second "works locally but differs in CI" generator world.
- Avoids Windows/macOS stub header rabbit hole.
- ppy/SDL3-CS also uses a Linux Docker flow for generation.

The ergonomics should be improved with:

- Cake host publish/cache reuse.
- Mounted repo root.
- Mounted `.vcpkg-cache`.
- Optional no-republish/reuse mode later.
- Easy final diff command.

### Option 2 — Multi-OS local CppAst runtime

Add Windows/macOS libclang runtime packages and allow local generation directly on each host.

Why this is risky:

- CppAst can run on multiple OSes, but system headers/sysroot behavior differs.
- SDL platform parsing may require stubs or SDK headers on non-Linux hosts.
- The output can drift from Linux-canonical CI generation.
- Any decision made from Windows/macOS local output may be based on the wrong AST.

Current recommendation: **do not add multi-OS production/local generation dependencies.** Keep generation Linux-container based, including local dev.

## Recommended Next Step

Before Task 4, design and implement a narrow local output loop:

1. Decide whether to pull Task 8's `tools.cs generate-bindings` forward, or implement a minimal precursor.
2. Keep production target Linux-canonical.
3. Make local output generation run through the same Linux builder container.
4. Generate a small real artifact early enough to inspect generated output before TypeMapping/Emitter work hardens.
5. Compare output shape against:
   - local CppAst spike artifacts under `tools/binding-spike/`;
   - ppy/SDL3-CS output style;
   - Alimer/Vortice typed handle style where relevant.

Only after that should Task 4 model/type mapping proceed.

## Current "Archive" Status

Archive/completed enough to carry forward:

- Onboarding.
- Build-host audit.
- Task 1 scaffold.
- Task 2 header/stamp boundary, after corrections.
- Task 3 platform catalog/parse options, after corrections.
- Maintenance playbook.
- Docs corrected to avoid misleading the next agent.

Not complete:

- No commit.
- No real generated SDL2.Core output yet.
- No `GenerateBindingsTask` yet.
- No `tools.cs generate-bindings` yet.
- No PreFlight stamp coherence validator yet.
- No model/type mapping/emitter output beyond setup/scaffolding.

## Things To Be Careful About

- Do not let plan snippets override ADR-002 / ADR-003.
- Do not create a `Pipeline` or custom `Runner` wrapper just because the old plan says so.
- Do not invent `BindingGenerationResult`.
- Do not reintroduce target-local `.generated-stamp` writer helpers.
- Do not move header input discovery back to Data just because it reads files.
- Do not copy SDL2 macro catalog to SDL3.
- Do not add Windows/macOS libclang runtime packages for generation without a deliberate design discussion.
- Do not run `git commit` unless Deniz explicitly asks.

## Useful Commands

```pwsh
git status --short
git diff --stat
dotnet build build/_build/Build.csproj -c Release
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"
```

Guard checks that were useful:

```pwsh
rg "HeaderSetRepository|IHeaderSetRepository|GeneratedStampWriter|BindingGenerationResult" build/_build docs
rg "DirectoryInfo|FileInfo|File\.|Directory\.|Path\.|System\.IO" build/_build/Targets/GenerateBindings build/_build.Tests/Unit/Targets/GenerateBindings
```

## Final Steering Note

The branch is in a better architectural state than mid-session, but it is still dirty and mid-slice. The next agent should not optimize for "more code fast." Optimize for a trustworthy local generation output loop first, then use that output to guide Task 4.
