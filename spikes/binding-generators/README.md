# Binding Generator Spikes

This folder hosts the SDL2 binding-generator prototypes that feed Phase 4 (binding auto-generation). The decision spike completed on 2026-05-21; the active prototype now follows **ppy-style ClangSharp + Microsoft.CodeAnalysis postprocess** with production-shaped multi-TFM library projects. See `output/reports/iteration-2-comparison.md` for the evidence and `docs/next-iteration-plan.md` for the slice-by-slice plan.

## Status — 2026-05-28

- **SDL2 Layer 1 raw ABI: CLOSED across Core / Image / GFX / TTF / Mixer** on the active ClangSharp + Roslyn postprocess implementation under `clangsharp/`. Framework targets come from the project files and [shared build properties](../../Directory.Build.props). Five-family oracle report at `output/reports/oracle-evidence-clangsharp.md`.
- **Durable policy, mechanism, maintenance and testing** live in the architecture and engineering homes linked below. [`docs/canonical/`](docs/canonical/) retains satellite analyses and references.
- **Forward scope:** [ROADMAP](../../docs/ROADMAP.md) owns current work; [promotion gates](../../docs/architecture/binding-generation.md#promotion-evidence-gates) own exit evidence. [Iteration tracking](docs/next-iteration-plan.md) remains spike-local.
- **Branch:** `spike/binding-autogen-sdl2-gfx`. Push gate pending Deniz approval per AGENTS.md §Approval Gate.

## Endgame Vision (read before making design decisions)

The spike is a **temporary proving ground**, not the final home. Once the ClangSharp + Roslyn postprocess pattern is proven across all 5 satellite families with solid Layer 1+2+3 + tests, the spike retires. Its production-worthy pieces are candidates for migration into the Cake build host pending separate M7 approval:

```
SPIKE (Python + C# console app)           →   PRODUCTION (unified Cake C# target)
────────────────────────────────────          ──────────────────────────────────
generate_bindings.py                         Future production generation target
  (Python orchestrator)              →         • ClangSharp tool wrapper (C#)
                                               • Postprocess rewriters inline
postprocess/Program.cs (CLI shell)   →         (same C# rewriter classes)
config/family-config.json            →       build/manifest.json
                                               (config absorbed, schema expanded)
rsp/*.rsp                            →       rsp/*.rsp (same files, same paths)
```

**Implications for spike work today:**
- Python code is transitional — don't over-invest in its architecture. It orchestrates ClangSharp; that's its only job.
- C# rewriter classes (postprocess) are durable — they are the strongest candidates for promotion into a Cake target. Invest in their design.
- Config decisions should anticipate manifest absorption as a candidate direction. `family-config.json` is data for a future M7 manifest-design review.
- Spike config that mirrors manifest shape is **intentional**, not redundant. It provides evidence for the schema before touching the production manifest.
- The old Cake generation target is removed. Tested salvage stays dormant at its existing paths; production integration remains a separate decision.


## Read first

| Doc | Why |
| --- | --- |
| **[AGENTS.md](../../AGENTS.md#task-routing)** | Task-to-canon routing. |
| **[`binding-abi-policy.md`](../../docs/architecture/binding-abi-policy.md)** | ABI/API/translation policy (pure principles). |
| **[ROADMAP](../../docs/ROADMAP.md)** | Current forward work. |
| **[`binding-testing.md`](../../docs/engineering/binding-testing.md)** | Testing layer model + smoke + raw ABI upstream port backlog. |
| **[`binding-generation.md`](../../docs/architecture/binding-generation.md)** | Mechanism + classification labels + hardcoding rules. |
| **[`binding-generator-maintenance.md`](../../docs/engineering/binding-generator-maintenance.md)** | Version-bump procedures + family-config.json schema + RSP maintenance. |
| **[`binding-output-review.md`](../../docs/engineering/binding-output-review.md)** | Multi-oracle review workflow. |
| **`docs/canonical/satellites/`** | Per-family + cross-family satellite analyses. |
| **`docs/canonical/references/ppy-reference-analysis-2026-05-25.md`** | ppy/SDL3-CS reference analysis. |
| `docs/next-iteration-plan.md` | Active spike plan (Layer 2 iteration + forward backlog). |
| [Reference-clone appendix](../../docs/engineering/binding-output-review.md#reference-clone-appendix) | Pinned comparison checkouts and clone workflow. |
| `output/reports/iteration-2-comparison.md` | Decision evidence (function counts, dynapi coherence, multi-TFM trajectory). |
| `output/reports/oracle-evidence-clangsharp.md` | Active family-aware raw ABI evidence. |
| `output/reports/clangsharp-failure-buckets.md` | 8 RSP-fix iteration history. |

## Decision recorded — 2026-05-21

**Direction:** ppy-style ClangSharp orchestrator (Python) **+** Microsoft.CodeAnalysis post-processor (C# console app) **+** production-shaped library projects (`Janset.SDL2.Core`, `Janset.SDL2.Image`, …) all under one `.sln`.

**Why ppy-style ClangSharp** (the dated comparison is preserved in `output/reports/iteration-2-comparison.md`):

- Multi-TFM dual codegen (`compatible-codegen` + `latest-codegen`) drove the legacy-TFM gap from 474 errors to 0 in a single Python flag.
- Owned generator surface stays small; the dated report records the output comparison against the now-removed host generator.

**What this prototype is NOT:**

- Not a production generator integration yet; the former host target is removed.
- Not a one-script monolith — Phase 4 splits orchestration (Python) from public-API projection (Microsoft.CodeAnalysis).
- Not Roslyn source generators — the grand plan (Constitution L41, Roadmap M7) calls for committed `.g.cs` files with `.generated-stamp` reproducibility, which is hard-coded build-time emission, not compile-time SG.

The Alimer-style scaffold and generated compile-check output are removed; the dated comparison remains in `output/reports/iteration-2-comparison.md`.

## Layout

```text
spikes/binding-generators/
├── README.md                                        # this file
├── BindingGeneratorSpikes.slnx                      # IDE solution covering current spike projects
├── Directory.Build.props                            # shared defaults, except frozen postprocess and oracle tooling
├── docs/
│   ├── next-iteration-plan.md                       # active spike iteration + forward backlog
│   └── canonical/                                   # satellite analyses and references
│       ├── satellites/                              # per-family + cross-family satellite analyses
│       └── references/                              # ppy/SDL3-CS reference analysis
├── clangsharp/                                      # ACTIVE — ppy-style ClangSharp prototype
│   ├── generate_bindings.py                         # complete-family orchestrator (Compat + Modern; multi-OS pass; 7-step postprocess)
│   ├── oracle.cs                                    # Roslyn/file-based raw ABI evidence reporter
│   ├── Janset.SDL2.ClangSharpSpike.slnx             # IDE solution (postprocess + 5-family src + AbiTests)
│   ├── config/
│   │   └── family-config.json                       # unified family configuration (identity, rosters, platform views, required surface)
│   ├── rsp/
│   │   ├── base.rsp                                 # cross-cutting policy (defines, remaps incl. wchar_t* → nint, with-types, clang_args)
│   │   ├── sdl2-core.rsp                            # family identity + exclusions for SDL2.Core
│   │   ├── sdl2-image.rsp                           # family identity + exclusions for SDL2_image
│   │   ├── sdl2-gfx.rsp                             # family identity + exclusions for SDL2_gfx
│   │   ├── sdl2-ttf.rsp                             # family identity + exclusions for SDL2_ttf
│   │   ├── sdl2-mixer.rsp                           # family identity + exclusions for SDL2_mixer
│   │   └── per-header/                              # ppy-pattern per-header RSP overlays (R6 tag remaps, BCL helper excludes, foreign-type boundary)
│   ├── postprocess/                                 # Microsoft.CodeAnalysis console app — 7 postprocess steps
│   ├── shims/platform-headers/                      # Windows-local synthetic platform parse shims only
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
│       ├── clangsharp-production.md                 # per-header production generation report
│       ├── oracle-evidence-clangsharp.md            # family-aware raw ABI evidence report
│       └── alimer-full.md                           # Alimer per-header generation report
└── references/                                      # GITIGNORED — local clones for evidence
    ├── ppy-SDL3-CS/                                 # north star for orchestrator pattern
    └── alimer-bindings-sdl/                         # CppAst reference patterns
```

## Quick commands

```pwsh
# Single-command spike generation: ClangSharp (compat + modern) × per-family + multi-OS pass + 7-step postprocess pipeline.
# Postprocess order (wired in generate_bindings.py): platform-delta → strip-varargs → libraryimport (Modern only) → flags-detect → guid-substitute → clong-dispatch → uniform-opaque.
# Outputs land under src/Janset.SDL2.<Family>/Generated/{Compat,Modern}/ including the canonical Handles.g.cs (Pattern B).
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims

# Multi-TFM compile-check (all spike projects / TFMs)
dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release

# Raw ABI oracle/evidence report (Roslyn, family-aware) — five-family
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report

# Per-TFM ABI runtime smoke
dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release
```

The 7-step postprocess pipeline lands all Priority C semantic-ABI policy in a single regen invocation. Compat runs the same conceptual order but skips the Modern-only `libraryimport` implementation detail:

1. **`platform-delta`** — multi-OS dedupe + guarded `[SupportedOSPlatform]` (Slice 3).
2. **`strip-varargs`** — Constitution L162-176 fmt-only enforcement (Slice 2).
3. **`libraryimport`** (Modern only) — `[DllImport]` → `[LibraryImport]` + `[UnmanagedCallConv]` (Slice 2).
4. **`flags-detect`** — `[System.Flags]` emission by `Flags` suffix plus family-keyed allow-list (Item 1 S1-6).
5. **`guid-substitute`** — `SDL_GUID` → `System.Guid` (Slice C-C).
6. **`clong-dispatch`** — R2 C `long` / `unsigned long` hybrid emit for Core and dormant TTF C `long` surfaces (Slice C-A + Item 1 S1-5).
7. **`uniform-opaque`** — R3/R4/R5 Pattern B by-value handle emit in `Handles.g.cs` + `SDL_X*` / satellite handle pointer rewrites at parameter / return / struct-field/function-pointer positions (Slice C-B + Item 1 S1-4).

`--use-platform-header-shims` keeps Windows-local spike generation moving by supplying minimal synthetic C headers for platform SDK includes that are unavailable on Windows (`endian.h`, `AvailabilityMacros.h`, `TargetConditionals.h`). Scoped to local iteration; production Linux evidence comes from the binding-generator docker container (`docker/binding-generator.Dockerfile`); other RIDs join on the per-RID CI matrix.

## Working rules

- Do not wire spike projects into production Cake targets, CI, package projects, or `build/manifest.json`.
- `Directory.Build.props` imports shared defaults for the binding and test projects; postprocess and the oracle retain their frozen settings.
- Reference repositories under `references/` are gitignored. Do not vendor them.
- Generated `.g.cs` outputs live under `clangsharp/src/Janset.SDL2.<Family>/Generated/` for the active prototype.
- ppy `generate_bindings.py` (~438 LOC) is the **north star** for the SDL2 orchestrator: per-header response files, manual-symbol exclusion feedback, sdl.json validation, multi-platform pass. Adapt these patterns; do not invent parallel mechanisms.
- Postprocess transforms belong in a `Microsoft.CodeAnalysis` C# console app (`clangsharp/postprocess/`). **Not** Roslyn source generators — Constitution requires committed output with reproducibility stamp. Current conceptual order: `platform-delta`, `strip-varargs`, `libraryimport` (Modern only), `flags-detect`, `guid-substitute`, `clong-dispatch`, `uniform-opaque`. Future Layer 2 typed-API projection lands as additional rewriters in the same app.
- Prefer direct, readable code over framework-shaped abstractions, but adapt ppy's mechanisms wholesale rather than reinvent.

## How a new agent should pick this up

1. [AGENTS.md](../../AGENTS.md#task-routing) — task-to-canon routing.
2. [`binding-abi-policy.md`](../../docs/architecture/binding-abi-policy.md) — ABI/API policy.
3. [ROADMAP](../../docs/ROADMAP.md) — current forward work; [promotion gates](../../docs/architecture/binding-generation.md#promotion-evidence-gates) — exit evidence.
4. [`binding-generation.md`](../../docs/architecture/binding-generation.md) — mechanism rationale.
5. [`binding-generator-maintenance.md`](../../docs/engineering/binding-generator-maintenance.md) — operational procedures.
6. [`binding-testing.md`](../../docs/engineering/binding-testing.md) — testing strategy.
7. [`binding-output-review.md`](../../docs/engineering/binding-output-review.md) — multi-oracle review.
8. `docs/next-iteration-plan.md` — active iteration + forward backlog.
9. `output/reports/iteration-2-comparison.md` + `oracle-evidence-clangsharp.md` — evidence.
10. `references/ppy-SDL3-CS/SDL3-CS/generate_bindings.py` (gitignored clone) — north-star orchestrator.

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

The container installs the SDK pinned by [global.json](../../global.json) and its bundled runtime. Executables that target an older framework than the SDK's own also need that framework's runtime at its current patch level. It exercises the Modern path; .NET Framework runtime checks run on Windows. Linux ARM64, macOS x64, and macOS ARM64 RID coverage joins on the CI per-RID matrix.
