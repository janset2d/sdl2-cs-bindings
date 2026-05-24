# Binding Generator Spikes

This folder hosts the SDL2 binding-generator prototypes that feed Phase 4 (binding auto-generation). The decision spike completed on 2026-05-21; the active prototype now follows **ppy-style ClangSharp + Microsoft.CodeAnalysis postprocess** with production-shaped multi-TFM library projects. See `output/reports/iteration-2-comparison.md` for the evidence and `docs/next-iteration-plan.md` for the slice-by-slice plan.

## Read first

| Doc | Why |
| --- | --- |
| **`.github/prompts/binding-generator-spike-handoff.prompt.md`** | **Reusable priming prompt** for a new LLM/agent entering both the repo and this spike. Use this when starting a fresh assistant session. |
| **`docs/llm-handoff.md`** | **LLM-to-LLM handoff — read this first.** Self-contained context dump: decision history, code map, slice progress, current sticking point, working preferences. |
| `docs/generator-spike-goals.md` | Spike charter — original goals, what got tested, and the recorded decision. |
| `docs/next-iteration-plan.md` | Active slice plan (1–5) covering the `clangsharp/` layout, multi-TFM postprocess, multi-OS pass, and satellite enablement. |
| `docs/oracle-evidence-design.md` | Spike-local design for replacing regex oracle comparison with a Roslyn/file-based-app evidence matrix. |
| `docs/oracle-evidence-implementation-plan.md` | Task-by-task implementation plan for the Roslyn/file-based-app oracle evidence slice. |
| `output/reports/iteration-2-comparison.md` | Decision-quality evidence: function counts, dynapi coherence, multi-TFM build trajectory, multi-OS gap. |
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
│   ├── oracle-evidence-design.md                    # Roslyn/file-based oracle evidence design
│   ├── oracle-evidence-implementation-plan.md       # oracle evidence task plan
│   └── reference-clones.md                          # local clone commands for upstream refs
├── scope/
│   ├── sdl2-core.headers.txt                        # full SDL2.Core header inventory (51 entries)
│   ├── sdl2-image.headers.txt                       # full SDL2_image header inventory
│   ├── bootstrap-sdl2-core.headers.txt              # bring-up slice (5 representative core headers)
│   └── bootstrap-sdl2-image.headers.txt             # bring-up slice (1 image header)
├── clangsharp/                                      # ACTIVE — ppy-style ClangSharp prototype
│   ├── generate_bindings.py                         # multi-pass orchestrator (codegen × family; multi-OS pass)
│   ├── compare_oracle.py                            # Cake-preview / dynapi comparison validator
│   ├── oracle.cs                                    # Roslyn/file-based raw ABI evidence reporter
│   └── rsp/
│       ├── base.rsp                                 # cross-cutting policy (defines, remaps, with-types, clang_args)
│       ├── sdl2-core.rsp                            # family identity + exclusions for SDL2.Core
│       └── sdl2-image.rsp                           # family identity + exclusions for SDL2_image
├── alimer-style/                                    # RETAINED reference — single-pass CppAst raw ABI emitter
│   └── src/Janset.Sdl2.AlimerSpike.Generator/
│   ├── src/
│   │   ├── Janset.SDL2.Core/                         # multi-TFM generated-output compile-check
│   │   └── Janset.SDL2.Image/                        # satellite generated-output compile-check
│   ├── shims/platform-headers/                       # Windows-local synthetic platform parse shims only
│   └── Janset.SDL2.ClangSharpSpike.slnx
├── output/
│   ├── alimer/                                      # Alimer-style raw ABI compile-check
│   └── reports/                                     # evidence + decision artifacts
│       ├── iteration-2-comparison.md                # comparison + decision-quality signal
│       ├── clangsharp-failure-buckets.md            # bucket map + RSP delta history
│       ├── clangsharp-full.md                       # per-header generation report (full scope)
│       ├── oracle-comparison-clangsharp.md          # ClangSharp vs Cake preview + dynapi
│       ├── oracle-comparison-alimer.md              # Alimer vs Cake preview + dynapi
│       ├── oracle-evidence-clangsharp.md            # family-aware raw ABI evidence report
│       ├── alimer-full.md                           # Alimer per-header generation report
│       └── clangsharp-bootstrap.md                  # bootstrap-scope generation report
└── references/                                      # GITIGNORED — local clones for evidence
    ├── ppy-SDL3-CS/                                 # north star for orchestrator pattern
    └── alimer-bindings-sdl/                         # CppAst reference patterns
```

## Quick commands

```pwsh
# Full-scope generation (both codegen passes; outputs land under Generated/Compat/ and Generated/Modern/)
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims

# Multi-TFM compile-check (all 5 TFMs)
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release

# Oracle comparison + dynapi coherence
python spikes/binding-generators/clangsharp/compare_oracle.py --approach clangsharp

# Raw ABI oracle/evidence report (Roslyn, family-aware)
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report
```

`--use-platform-header-shims` keeps Windows-local spike generation moving by supplying minimal synthetic C headers for platform SDK includes that are unavailable on Windows (`endian.h`, `AvailabilityMacros.h`, `TargetConditionals.h`). This is intentionally scoped to spike evidence; production platform evidence still comes from native Linux/macOS generation.

## Working rules

- Do not wire spike projects into production Cake targets, CI, package projects, or `build/manifest.json`.
- `Directory.Build.props` keeps spike projects isolated from root production packaging/analyzer/multi-TFM inheritance.
- Reference repositories under `references/` are gitignored. Do not vendor them.
- Generated `.g.cs` outputs live under `clangsharp/src/Janset.SDL2.<Family>/Generated/` for the active prototype.
- ppy `generate_bindings.py` (~438 LOC) is the **north star** for the SDL2 orchestrator: per-header response files, manual-symbol exclusion feedback, sdl.json validation, multi-platform pass. Adapt these patterns; do not invent parallel mechanisms.
- Postprocess transforms (`[DllImport]` → `[LibraryImport]`, `[SupportedOSPlatform]` `#if` guards, future public-typed projection) belong in a `Microsoft.CodeAnalysis` C# console app. **Not** Roslyn source generators — Constitution requires committed output with reproducibility stamp.
- Prefer direct, readable code over framework-shaped abstractions, but adapt ppy's mechanisms wholesale rather than reinvent.

## How a new agent should pick this up

**Start with [`.github/prompts/binding-generator-spike-handoff.prompt.md`](../../.github/prompts/binding-generator-spike-handoff.prompt.md)** when launching a fresh agent/session. Then read [`docs/llm-handoff.md`](docs/llm-handoff.md) for the full spike-specific context dump covering decision history, slice progress, current sticking point, working preferences, and pointers to every other doc + code path. After reading the handoff:

1. `docs/generator-spike-goals.md` for the original charter + recorded toolchain decision.
2. `docs/next-iteration-plan.md` for the active slice plan (1–5).
3. `output/reports/iteration-2-comparison.md` for the evidence base.
4. `output/reports/oracle-evidence-clangsharp.md` for the current family-aware raw ABI evidence snapshot.
5. `references/ppy-SDL3-CS/SDL3-CS/generate_bindings.py` (lines 232-365, 386-434) for the north-star orchestrator patterns.
6. Open `clangsharp/Janset.SDL2.ClangSharpSpike.slnx` in your IDE.

## Per-TFM ABI runtime smoke

**AbiTests (per-TFM ABI runtime smoke):** The `spikes/binding-generators/clangsharp/tests/abi-tests` project exercises Layer 1 `SDLNative.SDL_ThreadID()` runtime evidence per executable TFM (net462, net8.0, net9.0, net10.0). Host-side this covers Win32 32-bit `uint` and CULong+LibraryImport paths. Unix64 (Linux x64/arm64, macOS x64/arm64) `nint` returns must be exercised on the CI per-RID matrix by overriding the SDL2 native source path.
