# Binding Generator Spikes

This folder hosts the SDL2 binding-generator prototypes. The decision spike completed on 2026-05-21; its **ppy-style ClangSharp + Microsoft.CodeAnalysis postprocess** findings underpin the build-host migration, with production-shaped multi-TFM library projects retained here. See `output/reports/iteration-2-comparison.md` for the historical evidence and [ROADMAP](../../docs/ROADMAP.md) for current work.

## Status — 2026-11-14

- **SDL2 Layer 1 raw ABI: CLOSED across Core / Image / GFX / TTF / Mixer** on the retained ClangSharp-generated output under `clangsharp/`. Framework targets come from the project files and [shared build properties](../../Directory.Build.props). The former tracked five-family report is archived in the private companion repository; collect current evidence with the command below.
- **Durable policy, mechanism, maintenance and testing** live in the architecture and engineering homes linked below. [`docs/canonical/`](docs/canonical/) retains satellite analyses and references.
- **Forward scope:** [ROADMAP](../../docs/ROADMAP.md) owns current work; [promotion gates](../../docs/architecture/binding-generation.md#promotion-evidence-gates) own exit evidence. The [spike follow-up ledger](docs/next-iteration-plan.md) separates current tooling guidance and open items from historical foundation evidence.
- **Tooling:** `tools.cs generate-bindings` plans by default, generates with `--execute` and validates committed stamps with `--check`; postprocess and optional oracle evidence run in the build host.

## Endgame Vision (read before making design decisions)

The spike is a **temporary proving ground**, not the final home. The `GenerateBindings` Cake target
owns orchestration, staged generation and complete-family publication.
The [ClangSharp wrapper](../../build/_build/Tools/ClangSharp/) renders and captures one invocation.
[Response files](../../build/binding-generation/rsp/) and [platform-header shims](../../build/binding-generation/shims/)
now live beside the build manifest, unchanged. The [root tool manifest](../../dotnet-tools.json) pins ClangSharp.
The postprocess rewriters and their tests live in the [Cake build host](../../build/_build/Targets/GenerateBindings/PostProcess/)
as internal in-process collaborators, not a console app or a Roslyn source generator.
The [oracle](../../build/_build/Targets/GenerateBindings/Oracle/) and its tests also live in the host,
under its analyzers and central Roslyn version. The target's `--evidence` mode collects current,
non-mutating evidence; it does not regenerate bindings.

**Implications for spike work today:**
- C# postprocess rewriters are maintained and tested in the build host.
- Generator configuration lives in the [manifest](../../build/manifest.json); the build host resolves typed family profiles for postprocess.
- Global generation settings own family order and platform views; library profiles reuse existing package identities and library metadata.
- The CppAst generation target is retired. `GenerateBindings --execute` generates; `--evidence` consumes the hosted oracle and validators.


## Read first

| Doc | Why |
| --- | --- |
| **[AGENTS.md](../../AGENTS.md#task-routing)** | Task-to-canon routing. |
| **[`binding-abi-policy.md`](../../docs/architecture/binding-abi-policy.md)** | ABI/API/translation policy (pure principles). |
| **[ROADMAP](../../docs/ROADMAP.md)** | Current forward work. |
| **[`binding-testing.md`](../../docs/engineering/binding-testing.md)** | Testing layer model + smoke + raw ABI upstream port backlog. |
| **[`binding-generation.md`](../../docs/architecture/binding-generation.md)** | Mechanism + classification labels + hardcoding rules. |
| **[`binding-generator-maintenance.md`](../../docs/engineering/binding-generator-maintenance.md)** | Version-bump procedures + manifest generation profiles + RSP maintenance. |
| **[`binding-output-review.md`](../../docs/engineering/binding-output-review.md)** | Multi-oracle review workflow. |
| **`docs/canonical/satellites/`** | Per-family + cross-family satellite analyses. |
| **`docs/canonical/references/ppy-reference-analysis-2026-05-25.md`** | ppy/SDL3-CS reference analysis. |
| [Spike follow-up ledger](docs/next-iteration-plan.md) | Current tooling guidance, open items and labelled historical foundation evidence. |
| [Reference-clone appendix](../../docs/engineering/binding-output-review.md#reference-clone-appendix) | Pinned comparison checkouts and clone workflow. |
| `output/reports/iteration-2-comparison.md` | Decision evidence (function counts, dynapi coherence, multi-TFM trajectory). |
| `artifacts/binding-generation/oracle-evidence.md` (repository root, untracked) | Current family-aware raw ABI evidence. |
| `output/reports/clangsharp-failure-buckets.md` | 8 RSP-fix iteration history. |

## Decision recorded — 2026-05-21

**Historical direction (2026-05-21):** ppy-style ClangSharp orchestrator (Python) **+** Microsoft.CodeAnalysis post-processor (C# console app) **+** production-shaped library projects (`Janset.SDL2.Core`, `Janset.SDL2.Image`, …) all under one `.sln`. The orchestrator and console entry point are now retired; see the build-host arrangement above.

**Why ppy-style ClangSharp** (the dated comparison is preserved in `output/reports/iteration-2-comparison.md`):

- Multi-TFM dual codegen (`compatible-codegen` + `latest-codegen`) drove the legacy-TFM gap from 474 errors to 0 in a single Python flag.
- Owned generator surface stays small; the dated report records the output comparison against the now-removed host generator.

**What this prototype is NOT:**

- Not a production binding flip yet; the host target generates the retained spike projects with committed stamps and release/pack freshness checks.
- Not a one-script monolith — orchestration and public-API projection remain separate concepts in the build host.
- Not Roslyn source generators — the grand plan (Constitution L41, Roadmap M7) calls for committed `.g.cs` files with `.generated-stamp` reproducibility, which is hard-coded build-time emission, not compile-time SG.

The Alimer-style scaffold and generated compile-check output are removed; the dated comparison remains in `output/reports/iteration-2-comparison.md`.

## Layout

```text
spikes/binding-generators/
├── README.md                                        # this file
├── BindingGeneratorSpikes.slnx                      # IDE solution covering current spike projects
├── docs/
│   ├── next-iteration-plan.md                       # tooling, open follow-ups and historical foundation evidence
│   └── canonical/                                   # satellite analyses and references
│       ├── satellites/                              # per-family + cross-family satellite analyses
│       └── references/                              # ppy/SDL3-CS reference analysis
├── clangsharp/                                      # ACTIVE — ppy-style ClangSharp prototype
│   ├── Janset.SDL2.ClangSharpSpike.slnx             # IDE solution (5-family src + AbiTests)
│   ├── tests/abi-tests/                             # Per-TFM ABI runtime smoke; targets in AbiTests.csproj
│   └── src/
│       ├── Janset.SDL2.Core/                        # Support/DisableRuntimeMarshalling.cs; Generated/{Compat,Modern}/Handles.g.cs canonical home
│       ├── Janset.SDL2.Image/                       # ProjectReference -> Core; consumer mode for Pattern B handles
│       ├── Janset.SDL2.GFX/                         # ProjectReference -> Core
│       ├── Janset.SDL2.TTF/                         # ProjectReference -> Core
│       └── Janset.SDL2.Mixer/                       # ProjectReference -> Core
├── output/
│   └── reports/                                     # evidence + decision artifacts
│       ├── iteration-2-comparison.md                # comparison + decision-quality signal
│       ├── clangsharp-failure-buckets.md            # bucket map + RSP delta history
│       └── alimer-full.md                           # Alimer per-header generation report
└── references/                                      # GITIGNORED — local clones for evidence
    ├── ppy-SDL3-CS/                                 # north star for orchestrator pattern
    └── alimer-bindings-sdl/                         # CppAst reference patterns
```

## Quick commands

Use the provisioned checkout for generation; see [maintenance](../../docs/engineering/binding-generator-maintenance.md) for selection and guarantees:

```pwsh
# Read-only plan, no installed headers needed:
dotnet run --file tools.cs -- generate-bindings
# Complete generation; optionally repeat --scope with exact package-family IDs:
dotnet run --file tools.cs -- generate-bindings --execute --rid win-x64 --platform-header-shims
# Check committed output without installed headers:
dotnet run --file tools.cs -- generate-bindings --check
# Non-mutating evidence; optionally repeat --scope with exact package-family IDs.
dotnet run --file tools.cs -- generate-bindings --evidence

# Postprocess and oracle tests are part of the build-host suite.
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release

# Multi-TFM compile-check (all spike projects / TFMs)
dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release

# Per-TFM ABI runtime smoke
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
```

The [normalization sequence](../../docs/architecture/binding-generation.md#parser-and-backend-mechanism)
owns the nine-pass order and backend-specific behavior.

The [platform-header shims](../../build/binding-generation/shims/platform-headers/) supply minimal synthetic
C headers for platform SDK includes unavailable on Windows (`endian.h`, `AvailabilityMacros.h`,
`TargetConditionals.h`). They are local iteration aids, not production evidence; see
[native-platform evidence](../../docs/engineering/binding-generator-maintenance.md#linux-baked-image-evidence).

## Working rules

- Keep spike package and ABI-test projects separate from production packaging. The evidence target inspects the manifest-configured generated trees; generator machinery belongs in the host.
- Binding and test projects import the repository's shared build defaults directly.
- Reference repositories under `references/` are gitignored. Do not vendor them.
- Generated `.g.cs` outputs live under `clangsharp/src/Janset.SDL2.<Family>/Generated/` for the active prototype.
- ppy's `generate_bindings.py` (~438 LOC) is the peer reference for the orchestration patterns the `GenerateBindings` Cake target carries: per-header response files, manual-symbol exclusion feedback, sdl.json validation, multi-platform pass. Adapt these patterns in the build host; do not invent parallel mechanisms.
- Postprocess transforms belong in the [host's target-local collaborators](../../build/_build/Targets/GenerateBindings/PostProcess/), under its analyzers and central Roslyn version.
- Prefer direct, readable code over framework-shaped abstractions, but adapt ppy's mechanisms wholesale rather than reinvent.

## How a new agent should pick this up

1. [AGENTS.md](../../AGENTS.md#task-routing) — task-to-canon routing.
2. [`binding-abi-policy.md`](../../docs/architecture/binding-abi-policy.md) — ABI/API policy.
3. [ROADMAP](../../docs/ROADMAP.md) — current forward work; [promotion gates](../../docs/architecture/binding-generation.md#promotion-evidence-gates) — exit evidence.
4. [`binding-generation.md`](../../docs/architecture/binding-generation.md) — mechanism rationale.
5. [`binding-generator-maintenance.md`](../../docs/engineering/binding-generator-maintenance.md) — operational procedures.
6. [`binding-testing.md`](../../docs/engineering/binding-testing.md) — testing strategy.
7. [`binding-output-review.md`](../../docs/engineering/binding-output-review.md) — multi-oracle review.
8. [Spike follow-up ledger](docs/next-iteration-plan.md) — current tooling, open items and historical foundation evidence.
9. `output/reports/iteration-2-comparison.md` plus a fresh oracle artifact — historical and current evidence.
10. `references/ppy-SDL3-CS/SDL3-CS/generate_bindings.py` (gitignored clone) — peer reference for the orchestration patterns.

## Per-TFM ABI runtime smoke

**AbiTests (per-TFM ABI runtime smoke):** The [ABI test project](clangsharp/tests/abi-tests/AbiTests.csproj) exercises Layer 1 `SDLNative.SDL_ThreadID()` and `SDLNative.SDL_GetThreadID(SDL_Thread.Null)` runtime evidence per executable TFM. Its project file selects targets from the [shared build properties](../../Directory.Build.props). It consumes Core.Native through the existing smoke feed/version contract in `build/msbuild/Janset.Local.props`; provision that package with assets for the tested RID first.

Host-side Windows runs cover Win32 32-bit `uint` (Compat / .NET Framework) and 32-bit CULong+LibraryImport (Modern / .NET) paths.

Linux x64 CULong + LibraryImport ABI evidence uses the image built below. Rebuild after source changes. First provision the Core.Native package in the feed/version named by `build/msbuild/Janset.Local.props` through the existing local setup workflow. Copy that package folder into the disposable test container; source and vcpkg state stay baked in the image. No checkout bind mount, generation target or default entrypoint is used.

```pwsh
dotnet run --file tools.cs -- build-binding-generator-image
if ($LASTEXITCODE -ne 0) { throw 'Image build failed.' }
if (-not (Test-Path build/msbuild/Janset.Local.props -PathType Leaf)) {
    throw 'Local package properties file is missing. Provision Core.Native through local setup first.'
}
[xml]$local = Get-Content build/msbuild/Janset.Local.props -Raw
$feed = [string]$local.Project.PropertyGroup.LocalPackageFeed
$version = [string]$local.Project.PropertyGroup.JansetSdl2CorePackageVersion
if ([string]::IsNullOrWhiteSpace($feed) -or [string]::IsNullOrWhiteSpace($version)) {
    throw 'LocalPackageFeed and JansetSdl2CorePackageVersion must both be non-empty.'
}
if (-not (Test-Path -LiteralPath $feed -PathType Container)) {
    throw 'LocalPackageFeed folder does not exist. Provision Core.Native through local setup first.'
}
$container = docker create --entrypoint sh janset-binding-generator:focal-latest -c "cd /workspace/spikes/binding-generators/clangsharp/tests/abi-tests && dotnet test --project AbiTests.csproj -c Release --framework net10.0 -p:LocalPackageFeed=/feed -p:JansetSdl2CorePackageVersion=$version"
if ($LASTEXITCODE -ne 0) { throw 'Could not create ABI test container.' }
try {
    docker cp "$feed/." "${container}:/feed"
    if ($LASTEXITCODE -ne 0) { throw 'Could not copy package feed.' }
    docker start --attach $container
    $testExit = docker inspect --format '{{.State.ExitCode}}' $container
    if ($LASTEXITCODE -ne 0 -or $testExit -ne '0') { throw 'ABI test container failed.' }
}
finally {
    docker rm $container
}
```

The container installs the SDK pinned by [global.json](../../global.json), its bundled runtime, and the runtime of the older framework that the generator tools and the ABI tests run on. It exercises the Modern path; .NET Framework runtime checks run on Windows. Linux ARM64, macOS x64, and macOS ARM64 RID coverage joins on the CI per-RID matrix.
