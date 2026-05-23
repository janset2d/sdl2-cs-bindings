# Binding Auto-Generation Workstream

**Status (2026-05-23):** active binding-generator workstream under toolchain re-evaluation. The canonical *policy* documents are the constitution, grand roadmap, and testing strategy below. Historical specs, implementation transcripts, temporary notes, and superseded spike research were folded for unique current facts and removed from active docs; git history remains the archive.

> **Toolchain re-evaluation (2026-05-23):** The binding-autogen toolchain decision recorded in [ADR-004](../decisions/2026-05-14-binding-autogen-toolchain.md) is **Reopened** as of 2026-05-23. The active selection happens in [`spikes/binding-generators/`](../../spikes/binding-generators/) between ClangSharp + Roslyn postprocess and Alimer-style single-pass CppAst. Either path implies replacing the sunset Cake-hosted implementation under `build/_build/Targets/GenerateBindings/`. Policy in the constitution remains binding regardless; implementation-specific language in this README and the roadmap describes the sunset Cake implementation and will be rewritten when the spike concludes.

The sunset implementation lives at `build/_build/Targets/GenerateBindings/` (Cake-hosted, CppAst-based, Linux-canonical), configured per family through `build/manifest.json library_manifests[].binding_generation`. SDL2.Core has a generated internal ABI-shaped preview produced by that implementation. The active spike under `spikes/binding-generators/` carries the live implementation work for the toolchain re-evaluation; remaining work toward production (public typed wrappers, friendly overloads, production source flip, package-first smoke) lands on whichever implementation the spike selects.

## Folder Layout

| Path | Purpose |
| --- | --- |
| [`README.md`](README.md) | Workstream index and reading order. |
| [`binding-generator-constitution.md`](binding-generator-constitution.md) | Canonical binding-generator constitution: internal ABI, public API layers, C-to-C# translation, manifest config vs policy, and evidence gates. |
| [`binding-generator-roadmap.md`](binding-generator-roadmap.md) | Canonical grand roadmap: safety harness, architecture refactor, profiles, raw/public/friendly emission, SDL2 production flip, satellites, smoke expansion, and SDL3 extension. |
| [`testing-strategy.md`](testing-strategy.md) | Canonical generated-binding testing strategy: build-host tests, compile checks, NativeSmoke, PackageConsumerSmoke, asset-backed headless smoke, manual diagnostics, and fixture policy. |
| [`../parking-lot/binding-autogen-cake-implementation/`](../parking-lot/binding-autogen-cake-implementation/) | Archived detailed implementation plans (M1 safety harness, M2 topology refactor, M3 profile boundary) for the sunset Cake-hosted CppAst implementation. Plans completed against that sunset code; preserved for git-history continuity and architectural-intent reference. |
| [`../playbook/binding-generator-maintenance.md`](../playbook/binding-generator-maintenance.md) | In-progress maintenance playbook for platform macro catalogs, generated stamps, and overlay coupling. |
| [`../playbook/binding-output-oracle-validation.md`](../playbook/binding-output-oracle-validation.md) | Reusable multi-agent review workflow for validating generated output against official SDL sources, peer bindings, and .NET API evidence. |
| [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) | ADR-004 — original 2026-05-14 CppAst toolchain reasoning (Reopened 2026-05-23). |
| [`../../spikes/binding-generators/`](../../spikes/binding-generators/) | **Active toolchain selection spike.** ClangSharp + Roslyn postprocess vs Alimer-style single-pass CppAst comparison; outputs under `output/reports/`. Either path replaces the sunset Cake implementation. |
| [`../../spikes/binding-generators/docs/generator-spike-goals.md`](../../spikes/binding-generators/docs/generator-spike-goals.md) | Spike charter (original 2026-05-14; current state lives in spike docs and reports). |

## Reading Order

| # | Document | Purpose |
| --- | --- | --- |
| 1 | [`binding-generator-constitution.md`](binding-generator-constitution.md) | Read first. It defines the binding generator's ABI/API law and evidence gates. |
| 2 | [`binding-generator-roadmap.md`](binding-generator-roadmap.md) | Read second. It tracks the milestone plan from safety harness through SDL3. |
| 3 | [`testing-strategy.md`](testing-strategy.md) | Read before changing tests, snapshots, compile checks, smoke tests, fixtures, or manual diagnostics. |
| 4 | [`../parking-lot/binding-autogen-cake-implementation/`](../parking-lot/binding-autogen-cake-implementation/) | Read only for archived sunset-implementation reference (M1/M2/M3 plans). Not directive for current work. |
| 6 | [`../playbook/binding-generator-maintenance.md`](../playbook/binding-generator-maintenance.md) | Operational procedure for parser config, synthetic headers, stamps, upstream bumps, and hybrid-static coupling. |
| 7 | [`../playbook/binding-output-oracle-validation.md`](../playbook/binding-output-oracle-validation.md) | Output validation workflow against SDL headers, peer bindings, and .NET API evidence. |
| 8 | [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) | Toolchain ADR (Reopened). Read for the 2026-05-14 reasoning history; do not treat as binding while the spike is active. |
| 9 | [`../../spikes/binding-generators/`](../../spikes/binding-generators/) | **Read when working on the active toolchain re-evaluation.** Spike contains the live implementation work, comparison evidence, and selection criteria. |

Historical research and task-by-task plans were intentionally removed from active docs. Preserve new durable facts in the constitution, roadmap, or testing strategy instead of reviving one-off plan files.

## Current Decision Posture

- **Toolchain selection is open.** ADR-004 (Reopened 2026-05-23) recorded a 2026-05-14 CppAst decision; the active selection happens in [`spikes/binding-generators/`](../../spikes/binding-generators/) between ClangSharp + Roslyn postprocess and Alimer-style single-pass CppAst. Either path replaces the sunset Cake-hosted implementation under `build/_build/Targets/GenerateBindings/`.
- **Priority C closure design (2026-05-24)** at [`../superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md`](../superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md) closes Layer 1 raw ABI honesty on the active spike. Key decisions: (a) **uniform Pattern B typed handle structs** (`readonly partial struct X(nint value)`) emitted for every opaque handle including the previously-deferred `SDL_RWops` / `SDL_SysWMinfo` / `SDL_SysWMmsg`, used by value in raw ABI signatures (ABI-equivalent to `IntPtr` per Microsoft + SysV/AAPCS64/MSVC specs, type-safe at compile time); (b) C `long` hybrid (drop SDL_stdinc convenience helpers all-TFM, dual-dispatch `SDL_threadID` family on legacy TFMs); (c) shared `wchar_t*` opaque `nint` at raw ABI (opaque IS the ABI-correct mapping, not a Layer 3 friendly-layer repair); (d) per-header RSP organization migration aligning with ppy's pattern — `rsp/per-header/<header>.rsp` for per-header concerns, family RSP for family identity, base.rsp for cross-cutting. Toolchain-neutral policy decisions integrated back into the constitution.
- Public API shape is **internal raw ABI externs + public typed low-level API + friendly overloads** per [`binding-generator-constitution.md`](binding-generator-constitution.md). Internal means the raw container is not public API; generated methods may remain lexically public inside an internal container when using upstream emitters. Public raw `IntPtr` externs are not part of v1 preview; escape hatches belong in typed handles, `DangerousGetHandle()` / `nint`, span/pointer overloads, and deliberately unsafe APIs. SDL2-CS compatibility is best-effort: useful as an oracle, not the shape to freeze.
- Output class identity is family-based, manifest-driven, and not parse-view-based. SDL2 core currently uses namespace `SDL2`, public class `SDL`, and internal raw ABI class `SDLNative`; parse views produce files/attributes, not `Sdl2_Neutral` or `Sdl2_MacOS` classes.
- String-like SDL macro constants such as `SDL_HINT_*` use canonical `ReadOnlySpan<byte>` UTF-8 literal properties. String ergonomics is provided by method overloads; do not duplicate every macro as both `const string` and `ReadOnlySpan<byte>`.
- **Generator is build infrastructure.** Whichever toolchain the spike selects, the production implementation must remain build-host infrastructure (no consumer-build code generation, committed `.g.cs`, per-family `.generated-stamp`, validators reachable from PreFlight and Pack stages, local invocation via `tools.cs`). The sunset Cake-hosted implementation lived under `build/_build/Targets/GenerateBindings/` with cross-cutting validators under `build/_build/Validation/BindingGeneration/`.
- **Generator architecture separates ABI engine from SDL policy.** ClangSharp orchestrator + Roslyn postprocess and CppAst single-pass emitter are both viable engine shapes; the spike is selecting. Either way, the reusable core normalizes parsed declarations, builds semantic models, projects raw ABI, and emits deterministic file sets; SDL-specific choices (core-owned type maps, SDL2/SDL3 bool shape, native library identity, satellite reference rules) belong behind named policy/profile concepts. Keep target-local until a second real generator target earns promotion.
- **Refactor safety is snapshot- and fixture-first.** Before architectural refactors, capture a generated-output baseline and add embedded `.h` fixture coverage for high-risk ABI/model cases. Move/rename operations use `git mv`, and test topology moves with production topology.
- **Linux-canonical** generation for ABI correctness across the 7-RID surface. The sunset Cake implementation enforced this through a pinned `linux-builder` Docker container with `libclang.runtime.linux-x64` + `libClangSharp.runtime.linux-x64` pins; the active spike uses a similar Linux-canonical parse approach. Successor implementation may use a different mechanism only if it proves equivalent platform parse coverage.
- **Preprocessor-macro switching only** for platform passes. No `--target` cross-compile flag, no mingw-w64, no Apple SDK headers. SDL's public headers carry the cross-platform opaque-type forward declarations the parser needs. Verified against ppy/SDL3-CS Dockerfile + `generate_bindings.py` (WebFetch 2026-05-15) and the local CppAst spike.
- **`SDL_syswm.h` typed-union layout is Stage 2.** Stage 1 emits `SDL_GetWindowWMInfo` as a function with opaque `SDL_SysWMinfo*` parameter. The typed union with `[StructLayout(LayoutKind.Explicit, Size = 64)]` plus the small forward-declaration stub library (~15–20 types) lands in Stage 2.
- **SDL3 binding generation is gated on PD-7** (SDL2 real-public-release). The SDL3 vcpkg port + overlay triplet work is its own substantial scope and must not block SDL2 v1.0 stable.
- Toolchain re-evaluation under `spikes/binding-generators/` covers both directions: CppAst version-trio coupling and owned-emitter cost vs. ClangSharp orchestrator + Roslyn postprocess footprint. The spike's evidence matrix drives the final ADR amendment.
- ppy/SDL3-CS is the strongest SDL-specific reference for neutral + platform-specific passes (the ClangSharp spike adapts its orchestrator pattern). SkiaSharp is a strong CppAst discipline reference. Alimer.Bindings.SDL provides the comparison CppAst toolchain in the spike and useful C# shape ideas.
- Structural emission is generic AST-driven work. Name allowlists such as `Stage1StructNames` and `SDL_GameControllerButtonBind`-specific flattening are not canonical design; anonymous unions are translated from AST shape. `SDL_GUID` is explicitly mapped to `System.Guid` through substitution policy and is not emitted as a generated struct.

## Related Canonical Docs

- [`../phases/phase-4-binding-autogen.md`](../phases/phase-4-binding-autogen.md) — Phase 4 design brief.
- [`../release-strategy.md`](../release-strategy.md) — AST-first sequencing and v1.0 release strategy.
- [`../plan.md`](../plan.md) — roadmap and current phase status.
- [`../decisions/2026-05-05-target-centric-build-host.md`](../decisions/2026-05-05-target-centric-build-host.md) — ADR-002 target-centric build-host pattern the Cake-host fold inherits.
- [`../decisions/2026-05-12-build-host-data-layer.md`](../decisions/2026-05-12-build-host-data-layer.md) — ADR-003 contract-centric data layer pattern the binding validators extend.
- [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 binding-autogen toolchain (Reopened 2026-05-23).
- [`../../spikes/binding-generators/`](../../spikes/binding-generators/) — Active toolchain re-evaluation spike.
- [`../knowledge-base/testing-guidelines.md`](../knowledge-base/testing-guidelines.md) — canonical TUnit/MTP test infrastructure for generator tests.
- [`../knowledge-base/extraction-guidelines.md`](../knowledge-base/extraction-guidelines.md) — collaborator extraction discipline.
- [`../../AGENTS.md`](../../AGENTS.md) — operating rules and settled project decisions.
