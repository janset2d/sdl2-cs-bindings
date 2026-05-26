# Binding Generator Spikes

This folder hosts the SDL2 binding-generator prototypes that feed Phase 4 (binding auto-generation). The decision spike completed on 2026-05-21; the active prototype now follows **ppy-style ClangSharp + Microsoft.CodeAnalysis postprocess** with production-shaped multi-TFM library projects. See `output/reports/iteration-2-comparison.md` for the evidence and `docs/next-iteration-plan.md` for the slice-by-slice plan.

## Status — 2026-05-26

- **Priority C semantic-ABI: CLOSED.** All six known platform-sensitive ABI risks resolved (R1 `wchar_t*`, R2 C `long`/`unsigned long`, R3 `SDL_RWops`, R4 `SDL_SysWMinfo`, R5 `SDL_SysWMmsg`, R6 opaque-handle tag leak). Foreign Type Boundary Policy + BCL-Replaceable Helper Exclusion Policy + Cross-Assembly Pattern B contract (`[assembly: DisableRuntimeMarshalling]`) codified in the Constitution. Authoritative closure record: [`docs/priority-c-closure-summary.md`](docs/priority-c-closure-summary.md).
- **Item 1 per-library generation infrastructure: CLOSED 2026-05-26.** `--family all`, `--family core`, and `--family image` regenerate byte-stable Core/Image output; `selected_families("all")` intentionally stays `['core', 'image']`; TTF/Mixer/GFX metadata exists but generated output remains dormant/missing until Items 3/4/5 activate those families.
- **Evidence baseline.** Five-family oracle report has Core/Image clean and TTF/Mixer/GFX missing as expected; multi-TFM solution build clean; ABI smoke covers `SDL_ThreadID` plus `SDL_GetThreadID(SDL_Thread.Null)` across Windows `net462`/`net8.0`/`net9.0`/`net10.0`, with full 7-RID proof deferred to the production CI matrix.
- **Next forward scope:** **Iteration 2 config surface unification**, then Items 2–5 satellite Layer 1 completion (Image verification, GFX, TTF, Mixer). Layer 2 typed public API defers until all five families have stable Layer 1 raw ABI, providing a holistic view before designing the public projection. ppy reference analysis recorded in [`docs/ppy-reference-analysis-2026-05-25.md`](docs/ppy-reference-analysis-2026-05-25.md).
- **Branch:** `spike/binding-autogen-sdl2-gfx`. Push gate pending Deniz approval per AGENTS.md §Approval Gate.

## Read first

| Doc | Why |
| --- | --- |
| **`.github/prompts/binding-generator-spike-handoff.prompt.md`** | **Reusable priming prompt** for a new LLM/agent entering both the repo and this spike. Use this when starting a fresh assistant session. |
| **`docs/llm-handoff.md`** | **LLM-to-LLM handoff — read this first.** Self-contained context dump: decision history, code map, slice progress, current sticking point, working preferences. |
| **`docs/priority-c-closure-summary.md`** | **Priority C closure record** (2026-05-24). Six-risks resolution table, verification evidence, Foreign Type Boundary Policy, Cross-Assembly Pattern B contract. Read after the LLM handoff. |
| **`docs/ppy-reference-analysis-2026-05-25.md`** | **ppy/SDL3-CS reference analysis** (2026-05-25). Satellite generation architecture, companion class layer mapping, adaptation recommendations. |
| **`docs/satellites/sdl2-satellite-error-function-consolidation.md`** | **Satellite error function consolidation** (2026-05-26). Cross-family analysis of `#define` error macros, per-peer comparison, `.rsp` exclude table, companion-helper policy path. |
| **`docs/satellites/sdl2-function-like-macro-consolidation.md`** | **Function-like macro consolidation** (2026-05-26). Catalog of ~115 function-like C macros across all 6 families, Type A/B/C taxonomy, per-peer comparison, companion-helper policy recommendations. |
| `docs/generator-spike-goals.md` | Spike charter — original goals, what got tested, and the recorded decision. |
| `docs/next-iteration-plan.md` | Active spike plan; Priority A/B/C and Item 1 are closed; Iteration 2 config surface unification is next; Items 2–5 satellite Layer 1 completion are queued. Also owns the `Review Follow-up Backlog — 2026-05-25` triage sink distilled from the read-only reviewer reports. |
| `docs/testing/raw-abi-upstream-testing-spec.md` | Slice-local testing design for expanding the single `AbiTests.csproj` with curated SDL2 upstream pure, asset-backed, dummy-driver, and manual diagnostic coverage. |
| `docs/testing/raw-abi-upstream-testing-plan.md` | Task plan for implementing the raw ABI upstream testing stages inside the existing ClangSharp spike test project. |
| `docs/oracle-evidence-design.md` | Spike-local design for replacing regex oracle comparison with a Roslyn/file-based-app evidence matrix. |
| `docs/oracle-evidence-implementation-plan.md` | Task-by-task implementation plan for the Roslyn/file-based-app oracle evidence slice. |
| `output/reports/iteration-2-comparison.md` | Decision-quality evidence: function counts, dynapi coherence, multi-TFM build trajectory, multi-OS gap. |
| `output/reports/oracle-evidence-clangsharp.md` | Family-aware raw ABI evidence snapshot — Core/Image clean; dormant TTF/Mixer/GFX generated rows missing as expected. |
| `output/reports/clangsharp-failure-buckets.md` | Per-header failure bucket map + RSP delta history (8 RSP-fix cycles → compile-clean). |
| `docs/reference-clones.md` | Local clone commands for `ppy/SDL3-CS` and `amerkoleci/Alimer.Bindings.SDL`. References, not vendored deps. |

## Decision recorded — 2026-05-21

**Direction:** ppy-style ClangSharp orchestrator (Python) **+** Microsoft.CodeAnalysis post-processor (C# console app) **+** production-shaped library projects (`Janset.SDL2.Core`, `Janset.SDL2.Image`, …) all under one `.sln`.

**Why ppy-style ClangSharp over Alimer-style CppAst** (both reached compile-clean output during the spike — see comparison report):

- Multi-TFM dual codegen (`compatible-codegen` + `latest-codegen`) drove the legacy-TFM gap from 474 errors to 0 in a single Python flag.
- Owned generator surface stays small (1 Python orchestrator + 3 RSP files); structural ABI policy is shared with Cake's known-good output via the SDL2.exports dynapi (98.1% match).
- Cake's `ModelBuilding/` ~30-file CppAst pipeline solved the same problem with much more owned code. The spike showed the same hill can be climbed with a thin wrapper + downstream post-processor.

**What this prototype is NOT:**

- Not a replacement for Cake's `GenerateBindings` until Phase 4 production flip approves it.
- Not a one-script monolith — Phase 4 splits orchestration (Python) from public-API projection (Microsoft.CodeAnalysis).
- Not Roslyn source generators — the grand plan (Constitution L41, Roadmap M7) calls for committed `.g.cs` files with `.generated-stamp` reproducibility, which is hard-coded build-time emission, not compile-time SG.

**Alimer-style scaffold retained under `alimer-style/`** as a reference for CppAst patterns that may be useful if the ClangSharp toolchain hits a wall.

## Layout

```text
spikes/binding-generators/
├── README.md                                        # this file
├── BindingGeneratorSpikes.slnx                      # IDE solution covering current spike projects
├── Directory.Build.props                            # spike-local build defaults (CPM enabled, no production multi-TFM inheritance)
├── docs/
│   ├── generator-spike-goals.md                     # charter + recorded decision
│   ├── next-iteration-plan.md                       # active slice plan
│   ├── testing/                                      # slice-local raw ABI testing spec + plan
│   ├── oracle-evidence-design.md                    # Roslyn/file-based oracle evidence design
│   ├── oracle-evidence-implementation-plan.md       # oracle evidence task plan
│   └── reference-clones.md                          # local clone commands for upstream refs
├── scope/
│   ├── sdl2-core.headers.txt                        # full SDL2.Core header inventory (51 entries)
│   └── sdl2-image.headers.txt                       # full SDL2_image header inventory
├── clangsharp/                                      # ACTIVE — ppy-style ClangSharp prototype
│   ├── generate_bindings.py                         # complete-family orchestrator (Compat + Modern; multi-OS pass; 7-step postprocess)
│   ├── oracle.cs                                    # Roslyn/file-based raw ABI evidence reporter
│   ├── Janset.SDL2.ClangSharpSpike.slnx             # IDE solution (postprocess + Core + Image + AbiTests)
│   ├── rsp/
│   │   ├── base.rsp                                 # cross-cutting policy (defines, remaps incl. wchar_t* → nint, with-types, clang_args)
│   │   ├── sdl2-core.rsp                            # family identity + exclusions for SDL2.Core
│   │   ├── sdl2-image.rsp                           # family identity + exclusions for SDL2_image
│   │   └── per-header/                              # ppy-pattern per-header RSP overlays (R6 tag remaps, BCL helper excludes, foreign-type boundary)
│   ├── policy/
│   │   ├── opaque-handle-roster.json                # Pattern B handle roster, family-keyed schema 2.0
│   │   └── flags-enum-roster.json                   # [Flags] allow-list, family-keyed schema 2.0
│   ├── postprocess/                                 # Microsoft.CodeAnalysis console app — 7 postprocess steps
│   ├── shims/platform-headers/                      # Windows-local synthetic platform parse shims only
│   ├── tests/abi-tests/                             # Per-TFM ABI runtime smoke (net10/9/8/462, Win + Linux x64 docker)
│   └── src/
│       ├── Janset.SDL2.Core/                        # 5 TFMs; Support/DisableRuntimeMarshalling.cs; Generated/{Compat,Modern}/Handles.g.cs canonical home
│       └── Janset.SDL2.Image/                       # 5 TFMs; ProjectReference -> Core; consumer mode for Pattern B handles
├── alimer-style/                                    # RETAINED reference — single-pass CppAst raw ABI emitter
│   └── src/Janset.Sdl2.AlimerSpike.Generator/
├── output/
│   ├── alimer/                                      # Alimer-style raw ABI compile-check
│   └── reports/                                     # evidence + decision artifacts
│       ├── iteration-2-comparison.md                # comparison + decision-quality signal
│       ├── clangsharp-failure-buckets.md            # bucket map + RSP delta history
│       ├── clangsharp-production.md                 # per-header production generation report
│       ├── oracle-comparison-clangsharp.md          # legacy ClangSharp vs Cake preview report (not regenerated)
│       ├── oracle-comparison-alimer.md              # legacy Alimer vs Cake preview report (not regenerated)
│       ├── oracle-evidence-clangsharp.md            # family-aware raw ABI evidence report
│       └── alimer-full.md                           # Alimer per-header generation report
└── references/                                      # GITIGNORED — local clones for evidence
    ├── ppy-SDL3-CS/                                 # north star for orchestrator pattern
    └── alimer-bindings-sdl/                         # CppAst reference patterns
```

## Quick commands

```pwsh
# Single-command production generation: ClangSharp (compat + modern) × per-family + multi-OS pass + 7-step postprocess pipeline.
# Postprocess order (wired in generate_bindings.py): platform-delta → strip-varargs → libraryimport (Modern only) → flags-detect → guid-substitute → clong-dispatch → uniform-opaque.
# Outputs land under src/Janset.SDL2.<Family>/Generated/{Compat,Modern}/ including the canonical Handles.g.cs (Pattern B).
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims

# Multi-TFM compile-check (all spike projects / TFMs)
dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release

# Raw ABI oracle/evidence report (Roslyn, family-aware) — six-risks check
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
- `Directory.Build.props` keeps spike projects isolated from root production packaging/analyzer/multi-TFM inheritance.
- Reference repositories under `references/` are gitignored. Do not vendor them.
- Generated `.g.cs` outputs live under `clangsharp/src/Janset.SDL2.<Family>/Generated/` for the active prototype.
- ppy `generate_bindings.py` (~438 LOC) is the **north star** for the SDL2 orchestrator: per-header response files, manual-symbol exclusion feedback, sdl.json validation, multi-platform pass. Adapt these patterns; do not invent parallel mechanisms.
- Postprocess transforms belong in a `Microsoft.CodeAnalysis` C# console app (`clangsharp/postprocess/`). **Not** Roslyn source generators — Constitution requires committed output with reproducibility stamp. Current conceptual order: `platform-delta`, `strip-varargs`, `libraryimport` (Modern only), `flags-detect`, `guid-substitute`, `clong-dispatch`, `uniform-opaque`. Future Layer 2 typed-API projection lands as additional rewriters in the same app.
- Prefer direct, readable code over framework-shaped abstractions, but adapt ppy's mechanisms wholesale rather than reinvent.

## How a new agent should pick this up

**Start with [`.github/prompts/binding-generator-spike-handoff.prompt.md`](../../.github/prompts/binding-generator-spike-handoff.prompt.md)** when launching a fresh agent/session. Then read [`docs/llm-handoff.md`](docs/llm-handoff.md) for the full spike-specific context dump covering decision history, slice progress, current sticking point, working preferences, and pointers to every other doc + code path. After reading the handoff:

1. `docs/priority-c-closure-summary.md` for the **authoritative Priority C closure record** — six-risks resolution table, verification evidence, Foreign Type Boundary Policy, Cross-Assembly Pattern B contract. This anchors the current Layer 1 evidence baseline.
2. `docs/generator-spike-goals.md` for the original charter + recorded toolchain decision.
3. `docs/next-iteration-plan.md` for the active spike plan; Iteration 2 config surface unification is next, with Items 2–5 satellite Layer 1 completion queued after it.
4. `output/reports/iteration-2-comparison.md` for the evidence base.
5. `output/reports/oracle-evidence-clangsharp.md` for the current family-aware raw ABI evidence snapshot; Core/Image are clean and dormant TTF/Mixer/GFX generated rows are missing as expected.
6. `references/ppy-SDL3-CS/SDL3-CS/generate_bindings.py` (lines 232-365, 386-434) for the north-star orchestrator patterns.
7. Open `clangsharp/Janset.SDL2.ClangSharpSpike.slnx` in your IDE.

## Per-TFM ABI runtime smoke

**AbiTests (per-TFM ABI runtime smoke):** The `spikes/binding-generators/clangsharp/tests/abi-tests` project exercises Layer 1 `SDLNative.SDL_ThreadID()` and `SDLNative.SDL_GetThreadID(SDL_Thread.Null)` runtime evidence per executable TFM (net462, net8.0, net9.0, net10.0). The csproj OS-dispatches the native lib copy: Windows pulls `vcpkg_installed/x64-windows-hybrid/bin/SDL2.dll`; Linux pulls `vcpkg_installed/x64-linux-hybrid/lib/libSDL2-2.0.so.0` (also copied as `libSDL2.so` so the .NET name fallback resolves it without ldconfig).

Host-side Windows run covers Win32 32-bit `uint` (Compat / net462) and 32-bit CULong+LibraryImport (Modern / net8+) paths.

Linux x64 64-bit CULong+LibraryImport path is exercised locally via the binding-generator Docker container (which bakes vcpkg_installed/x64-linux-hybrid into the image; see `docker/binding-generator.Dockerfile` and `tools.cs:GenerateBindingsCommand` for the build args). Command override pattern (image already built):

```bash
docker run --rm --entrypoint sh janset-binding-generator:focal-latest \
  -c "cd /workspace/spikes/binding-generators/clangsharp/tests/abi-tests && \
      dotnet test --project AbiTests.csproj -c Release --framework net10.0"
```

`net8.0` / `net9.0` runtimes are not present in the container today (Dockerfile Layer A installs only the SDK pinned by `global.json` plus its bundled runtime). The Modern path is single-source so `net10.0` runtime evidence covers it. Linux ARM64, macOS x64, and macOS ARM64 RID coverage joins on the CI per-RID matrix.
